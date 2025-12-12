using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace GramCloneClient.Services
{
    public class TextFocusObserver : IDisposable
    {
        private AutomationElement? _lastFocusedElement;
        private TextPattern? _lastTextPattern;  // Cache the TextPattern for GetErrorRects
        private CancellationTokenSource? _pollingCts;
        private string _lastObservedText = string.Empty;
        private Rect _lastBounds = Rect.Empty;
        private Rect _lastCaretBounds = Rect.Empty;

        // Store element info for application-specific replacement strategies
        private string _lastClassName = string.Empty;
        private string _lastProcessName = string.Empty;

        // Configurable polling interval (default 150ms, range 100-500ms)
        private int _pollingIntervalMs = 150;

        public event EventHandler<(string Text, Rect ElementBounds, Rect CaretBounds)>? TextChanged;
        public event EventHandler<FocusDiagnostics>? DiagnosticsUpdated;

        public TextFocusObserver()
        {
        }
        
        // ... (existing code) ...

        private (string Text, Rect ElementBounds, Rect CaretBounds) ReadTextAndBounds(AutomationElement element)
        {
            string text = string.Empty;
            Rect bounds = Rect.Empty;
            Rect caretBounds = Rect.Empty;
            bool hasTextPattern = false;
            string controlType = element.Current.ControlType.LocalizedControlType;

            try
            {
                bounds = element.Current.BoundingRectangle;

                object patternObj;
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                {
                    hasTextPattern = true;
                    var textPattern = (TextPattern)patternObj;
                    _lastTextPattern = textPattern;  // Cache for GetErrorRects
                    var documentRange = textPattern.DocumentRange;
                    text = documentRange.GetText(2000);

                    // Try to get caret position
                    var selections = textPattern.GetSelection();
                    if (selections.Length > 0)
                    {
                        var selection = selections[0];
                        var rects = selection.GetBoundingRectangles();
                        if (rects.Length > 0)
                        {
                            caretBounds = rects[0];
                        }
                    }
                }
                else if (element.TryGetCurrentPattern(ValuePattern.Pattern, out patternObj))
                {
                    var valuePattern = (ValuePattern)patternObj;
                    text = valuePattern.Current.Value;
                }
                
                // Fire diagnostics event
                DiagnosticsUpdated?.Invoke(this, new FocusDiagnostics
                {
                    ProcessName = _lastProcessName,
                    ControlType = controlType,
                    HasTextPattern = hasTextPattern,
                    Bounds = bounds
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read text/bounds: {ex.Message}");
            }
            return (text, bounds, caretBounds);
        }

        // ... (rest of class) ...
    }

    public class FocusDiagnostics
    {
        public string ProcessName { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
        public bool HasTextPattern { get; set; }
        public Rect Bounds { get; set; }
    }
}

        /// <summary>
        /// Get screen rectangles for text at specified offset and length.
        /// Used for drawing error underlines in the overlay.
        /// </summary>
        public List<Rect> GetErrorRects(int offset, int length)
        {
            var rects = new List<Rect>();

            if (_lastTextPattern == null)
            {
                Logger.Log("GetErrorRects: No cached TextPattern available");
                return rects;
            }

            try
            {
                var documentRange = _lastTextPattern.DocumentRange;

                // Get the full text to verify offset/length
                string fullText = documentRange.GetText(2000);
                int docLength = fullText.Length;
                Logger.Log($"GetErrorRects: Document has {docLength} chars, seeking offset={offset}, length={length}");

                if (offset >= docLength)
                {
                    Logger.Log("GetErrorRects: offset beyond document length");
                    return rects;
                }

                // Clone the document range (starts as full document: 0 to docLength)
                var errorRange = documentRange.Clone();

                // Strategy: Move START forward by offset, then move END backward to leave only 'length' chars
                // Step 1: Move start forward by offset characters
                int movedStart = errorRange.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.Start,
                    TextUnit.Character,
                    offset);

                // Step 2: Move end backward to be exactly 'length' chars from start
                // Current end is at docLength, we want it at offset + length
                // So move backward by (docLength - offset - length)
                int charsToMoveBack = docLength - offset - length;
                int movedEnd = errorRange.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.End,
                    TextUnit.Character,
                    -charsToMoveBack);

                Logger.Log($"GetErrorRects: Moved start by {movedStart}, moved end by {movedEnd}");

                // Verify what text is selected
                string selectedText = errorRange.GetText(100);
                Logger.Log($"GetErrorRects: Selected text = '{selectedText}'");

                // Get bounding rectangles for the error range
                var boundingRects = errorRange.GetBoundingRectangles();
                Logger.Log($"GetErrorRects: Got {boundingRects.Length} bounding rectangles");

                foreach (var r in boundingRects)
                {
                    Logger.Log($"GetErrorRects: Raw rect = X:{r.X}, Y:{r.Y}, W:{r.Width}, H:{r.Height}");
                    if (r.Width > 0 && r.Height > 0)
                    {
                        rects.Add(r);
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                Logger.Log("GetErrorRects: Element not available");
            }
            catch (Exception ex)
            {
                Logger.Log($"GetErrorRects failed: {ex.Message}");
            }

            return rects;
        }

        /// <summary>
        /// Replace text at specified offset with replacement string.
        /// Uses a fallback chain: ValuePattern -> Word COM -> Clipboard+Paste
        /// </summary>
        public void ReplaceText(int offset, int length, string replacement)
        {
            if (_lastFocusedElement == null)
            {
                Logger.Log("ReplaceText: No cached element");
                return;
            }

            Logger.Log($"ReplaceText: Attempting replacement at offset={offset}, length={length}, class='{_lastClassName}', process='{_lastProcessName}'");

            // Strategy 1: Try ValuePattern (simplest, most reliable for supported controls)
            if (TryReplaceViaValuePattern(offset, length, replacement))
            {
                Logger.Log("ReplaceText: SUCCESS via ValuePattern");
                return;
            }

            // Strategy 2: Try application-specific COM (Word, Excel, etc.)
            if (TryReplaceViaComAutomation(offset, length, replacement))
            {
                Logger.Log("ReplaceText: SUCCESS via COM Automation");
                return;
            }

            // Strategy 3: Fall back to clipboard + keyboard simulation
            Logger.Log("ReplaceText: Falling back to clipboard approach");
            ReplaceViaClipboard(offset, length, replacement);
        }

        /// <summary>
        /// Strategy 1: Replace text using ValuePattern.SetValue() - works for Notepad, simple text boxes
        /// </summary>
        private bool TryReplaceViaValuePattern(int offset, int length, string replacement)
        {
            if (_lastFocusedElement == null) return false;

            try
            {
                if (_lastFocusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj))
                {
                    var valuePattern = (ValuePattern)patternObj;

                    // Check if the control is read-only
                    if (valuePattern.Current.IsReadOnly)
                    {
                        Logger.Log("TryReplaceViaValuePattern: Control is read-only");
                        return false;
                    }

                    // Get current value, modify, and set back
                    string currentValue = valuePattern.Current.Value ?? string.Empty;

                    if (offset > currentValue.Length)
                    {
                        Logger.Log($"TryReplaceViaValuePattern: offset {offset} > length {currentValue.Length}");
                        return false;
                    }

                    // Clamp length to avoid overflow
                    int actualLength = Math.Min(length, currentValue.Length - offset);

                    string newValue = currentValue.Remove(offset, actualLength).Insert(offset, replacement);

                    Logger.Log($"TryReplaceViaValuePattern: Replacing '{currentValue.Substring(offset, actualLength)}' with '{replacement}'");

                    valuePattern.SetValue(newValue);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"TryReplaceViaValuePattern: Failed - {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Strategy 2: Replace text using COM automation for Word
        /// </summary>
        private bool TryReplaceViaComAutomation(int offset, int length, string replacement)
        {
            // Only attempt for Microsoft Word
            bool isWord = _lastClassName == "_WwG" ||
                          _lastProcessName.Equals("WINWORD", StringComparison.OrdinalIgnoreCase);

            if (!isWord)
            {
                Logger.Log("TryReplaceViaComAutomation: Not a Word document, skipping");
                return false;
            }

            try
            {
                Logger.Log("TryReplaceViaComAutomation: Attempting Word COM automation");

                // Get running Word instance via COM (late binding)
                // .NET Core doesn't have Marshal.GetActiveObject, so we use our helper
                dynamic? wordApp = GetActiveObject("Word.Application");
                if (wordApp == null)
                {
                    Logger.Log("TryReplaceViaComAutomation: Could not get Word.Application");
                    return false;
                }

                dynamic doc = wordApp.ActiveDocument;

                // Create a range at the specified position
                // Note: Word Range uses 0-based indexing for Start, but might have different offset calculation
                dynamic range = doc.Range(offset, offset + length);

                // Log what we're about to replace
                string existingText = range.Text;
                Logger.Log($"TryReplaceViaComAutomation: Word range text = '{existingText}', replacing with '{replacement}'");

                // Replace the text
                range.Text = replacement;

                Logger.Log("TryReplaceViaComAutomation: Word replacement successful");
                return true;
            }
            catch (COMException ex)
            {
                Logger.Log($"TryReplaceViaComAutomation: COM error - {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.Log($"TryReplaceViaComAutomation: Failed - {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Helper to get an active COM object by ProgID (.NET Core doesn't have Marshal.GetActiveObject)
        /// </summary>
        private static object? GetActiveObject(string progId)
        {
            Guid clsid;
            int hr = CLSIDFromProgID(progId, out clsid);
            if (hr < 0)
                return null;

            hr = GetActiveObject(ref clsid, IntPtr.Zero, out object? obj);
            if (hr < 0)
                return null;

            return obj;
        }

        [DllImport("ole32.dll")]
        private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);

        /// <summary>
        /// Strategy 3: Replace text using clipboard and keyboard simulation (fallback)
        /// Uses GetAncestor to get the correct top-level window handle
        /// </summary>
        private void ReplaceViaClipboard(int offset, int length, string replacement)
        {
            if (_lastTextPattern == null || _lastFocusedElement == null)
            {
                Logger.Log("ReplaceViaClipboard: No cached TextPattern or element");
                return;
            }

            try
            {
                var documentRange = _lastTextPattern.DocumentRange;
                var selectRange = documentRange.Clone();

                // Get full text to navigate correctly
                string fullText = documentRange.GetText(10000);
                int docLength = fullText.Length;

                if (offset >= docLength)
                {
                    Logger.Log($"ReplaceViaClipboard: offset {offset} beyond document length {docLength}");
                    return;
                }

                // Navigate to the error position
                selectRange.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, offset);
                int charsToMoveBack = docLength - offset - length;
                selectRange.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -charsToMoveBack);

                // Verify selection
                string selectedText = selectRange.GetText(100);
                Logger.Log($"ReplaceViaClipboard: About to select '{selectedText}' and replace with '{replacement}'");

                // Get the window handle and find the TOP-LEVEL window using GetAncestor
                var childHwnd = IntPtr.Zero;
                var topLevelHwnd = IntPtr.Zero;

                try
                {
                    // First, get any window handle we can find
                    try
                    {
                        childHwnd = new IntPtr(_lastFocusedElement.Current.NativeWindowHandle);
                    }
                    catch { }

                    // Walk up the UI Automation tree if needed
                    if (childHwnd == IntPtr.Zero)
                    {
                        var walker = TreeWalker.ControlViewWalker;
                        var parent = _lastFocusedElement;
                        while (parent != null && childHwnd == IntPtr.Zero)
                        {
                            try
                            {
                                int nativeHandle = parent.Current.NativeWindowHandle;
                                if (nativeHandle != 0)
                                    childHwnd = new IntPtr(nativeHandle);
                            }
                            catch { }
                            parent = walker.GetParent(parent);
                        }
                    }

                    // CRITICAL: Use GetAncestor to get the TOP-LEVEL window, not the child/document element
                    if (childHwnd != IntPtr.Zero)
                    {
                        topLevelHwnd = NativeMethods.GetAncestor(childHwnd, NativeMethods.GA_ROOT);
                        Logger.Log($"ReplaceViaClipboard: Child hwnd={childHwnd}, Top-level hwnd={topLevelHwnd}");
                    }

                    if (topLevelHwnd != IntPtr.Zero)
                    {
                        // Use ForceForegroundWindow on the TOP-LEVEL window
                        NativeMethods.ForceForegroundWindow(topLevelHwnd);
                        Logger.Log($"ReplaceViaClipboard: ForceForegroundWindow to top-level {topLevelHwnd}");
                    }
                    else
                    {
                        Logger.Log("ReplaceViaClipboard: Could not get top-level window handle");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"ReplaceViaClipboard: ForceForegroundWindow failed: {ex.Message}");
                }

                // Wait for window activation to complete
                Thread.Sleep(150);

                // Also try UIAutomation SetFocus for the specific text element
                try
                {
                    _lastFocusedElement.SetFocus();
                    Logger.Log("ReplaceViaClipboard: UIAutomation SetFocus succeeded");
                }
                catch (Exception ex)
                {
                    Logger.Log($"ReplaceViaClipboard: UIAutomation SetFocus failed (non-fatal): {ex.Message}");
                }

                // Wait for focus to settle
                Thread.Sleep(100);

                // Verify foreground window is correct before proceeding
                var currentForeground = NativeMethods.GetForegroundWindow();
                Logger.Log($"ReplaceViaClipboard: Current foreground={currentForeground}, target top-level={topLevelHwnd}");

                // Check if we got the right window
                if (topLevelHwnd != IntPtr.Zero && currentForeground != topLevelHwnd)
                {
                    Logger.Log("ReplaceViaClipboard: WARNING - Foreground window doesn't match target!");
                }

                // Select the error text in the target application
                selectRange.Select();
                Logger.Log("ReplaceViaClipboard: Text selection applied");

                // Wait for selection to be applied
                Thread.Sleep(150);

                // Use clipboard to replace
                System.Windows.Clipboard.SetText(replacement);
                Logger.Log($"ReplaceViaClipboard: Clipboard set with '{replacement}'");

                // Wait before paste
                Thread.Sleep(100);

                // Send Ctrl+V to paste
                NativeMethods.SendPasteShortcut();
                Logger.Log("ReplaceViaClipboard: Paste shortcut sent");

                // Wait for paste to complete
                Thread.Sleep(100);

                Logger.Log("ReplaceViaClipboard: Replacement completed");
            }
            catch (Exception ex)
            {
                Logger.Log($"ReplaceViaClipboard failed: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
