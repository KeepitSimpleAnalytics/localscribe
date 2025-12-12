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

        public TextFocusObserver()
        {
        }

        /// <summary>
        /// Set the text polling interval in milliseconds.
        /// Values are clamped to 100-500ms range.
        /// </summary>
        /// <param name="intervalMs">Polling interval in milliseconds.</param>
        public void SetPollingInterval(int intervalMs)
        {
            // Clamp to safe range
            _pollingIntervalMs = Math.Max(100, Math.Min(500, intervalMs));
            Logger.Log($"TextFocusObserver: Polling interval set to {_pollingIntervalMs}ms");
        }

        /// <summary>
        /// Determines if a Document control type is editable.
        /// Uses multiple detection strategies to filter out read-only web pages
        /// while keeping editable documents (Word, Google Docs, etc.) working.
        /// </summary>
        private bool IsDocumentEditable(AutomationElement element)
        {
            try
            {
                // Strategy 1: Check TextPattern.IsReadOnlyAttribute (most reliable for documents)
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObj))
                {
                    var textPattern = (TextPattern)textPatternObj;
                    var documentRange = textPattern.DocumentRange;

                    object isReadOnlyAttr = documentRange.GetAttributeValue(TextPattern.IsReadOnlyAttribute);

                    if (isReadOnlyAttr == TextPattern.MixedAttributeValue)
                    {
                        // Document has both read-only and editable regions (e.g., contenteditable in page)
                        Logger.Log("IsDocumentEditable: MixedAttributeValue - treating as editable");
                        return true;
                    }
                    else if (isReadOnlyAttr != AutomationElement.NotSupported && isReadOnlyAttr is bool isReadOnly)
                    {
                        Logger.Log($"IsDocumentEditable: TextPattern.IsReadOnlyAttribute = {isReadOnly}");
                        return !isReadOnly;
                    }
                }

                // Strategy 2: Check ValuePattern.IsReadOnly (fallback for some controls)
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObj))
                {
                    var valuePattern = (ValuePattern)valuePatternObj;
                    bool isReadOnly = valuePattern.Current.IsReadOnly;
                    Logger.Log($"IsDocumentEditable: ValuePattern.IsReadOnly = {isReadOnly}");
                    return !isReadOnly;
                }

                // Strategy 3: Check IsKeyboardFocusable property
                bool isKeyboardFocusable = element.Current.IsKeyboardFocusable;
                Logger.Log($"IsDocumentEditable: IsKeyboardFocusable = {isKeyboardFocusable}");

                if (!isKeyboardFocusable)
                {
                    return false;
                }

                // Strategy 4: Check for known application processes
                string processName = _lastProcessName.ToUpperInvariant();

                // Known editor applications - always check these
                bool isKnownEditorProcess = processName switch
                {
                    "WINWORD" => true,      // Microsoft Word
                    "EXCEL" => true,        // Microsoft Excel
                    "POWERPNT" => true,     // Microsoft PowerPoint
                    "ONENOTE" => true,      // Microsoft OneNote
                    "SOFFICE" => true,      // LibreOffice
                    "NOTEPAD++" => true,    // Notepad++
                    "CODE" => true,         // VS Code
                    "DEVENV" => true,       // Visual Studio
                    "SUBLIME_TEXT" => true, // Sublime Text
                    "ATOM" => true,         // Atom
                    _ => false
                };

                if (isKnownEditorProcess)
                {
                    Logger.Log($"IsDocumentEditable: Known editor process '{processName}' - allowing");
                    return true;
                }

                // Known browser processes - default to NOT editable for Document controls
                bool isBrowserProcess = processName switch
                {
                    "CHROME" => true,
                    "FIREFOX" => true,
                    "MSEDGE" => true,
                    "IEXPLORE" => true,
                    "OPERA" => true,
                    "BRAVE" => true,
                    "VIVALDI" => true,
                    "CHROMIUM" => true,
                    _ => false
                };

                if (isBrowserProcess)
                {
                    // For browser Documents, if we couldn't determine editability through patterns,
                    // default to NOT editable to prevent grammar checking on rendered web pages
                    Logger.Log($"IsDocumentEditable: Browser process '{processName}' - defaulting to NOT editable");
                    return false;
                }

                // Unknown application with keyboard focus - assume editable
                Logger.Log($"IsDocumentEditable: Unknown process '{processName}' with keyboard focus - assuming editable");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"IsDocumentEditable: Exception - {ex.Message}");
                // On error, default to NOT editable for safety
                return false;
            }
        }

        public void Start()
        {
            try
            {
                Automation.AddAutomationFocusChangedEventHandler(OnFocusChanged);
                Logger.Log("TextFocusObserver: Started monitoring focus.");
            }
            catch (Exception ex)
            {
                Logger.Log($"TextFocusObserver: Failed to start. {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                StopPolling();
                Automation.RemoveAutomationFocusChangedEventHandler(OnFocusChanged);
                Logger.Log("TextFocusObserver: Stopped monitoring focus.");
            }
            catch (Exception ex)
            {
                Logger.Log($"TextFocusObserver: Failed to stop. {ex.Message}");
            }
        }

        private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
        {
            try
            {
                var element = sender as AutomationElement;
                if (element == null) return;

                // Get element info
                var controlType = element.Current.ControlType;
                string className = element.Current.ClassName;
                string name = element.Current.Name;

                Logger.Log($"Focus: Name='{name}', Type='{controlType.LocalizedControlType}', Class='{className}'");

                // Ignore focus changes to our own windows AND their child elements
                // Check window name OR if it's a child of our windows (dataitem, list item, button inside tooltip)
                bool isOurWindow = name == "Error Tooltip" || name == "LocalScribe Bubble" ||
                                   name == "LocalScribe Overlay" || name?.StartsWith("LocalScribe") == true;

                // Also filter child elements inside our windows (suggestion buttons, list items, text boxes, etc.)
                bool isChildOfOurWindow = controlType == ControlType.DataItem ||
                                          controlType == ControlType.ListItem ||
                                          controlType == ControlType.Button ||
                                          controlType == ControlType.Edit ||    // TextBox controls
                                          className == "ItemsControlItem" ||
                                          className == "TextBox" ||             // WPF TextBox
                                          className == "RichTextBox";           // WPF RichTextBox

                // Check if parent window is one of ours
                if (!isOurWindow && isChildOfOurWindow)
                {
                    try
                    {
                        var parent = TreeWalker.ControlViewWalker.GetParent(element);
                        while (parent != null)
                        {
                            string parentName = parent.Current.Name;
                            if (parentName == "Error Tooltip" || parentName == "LocalScribe Bubble" ||
                                parentName?.StartsWith("LocalScribe") == true)
                            {
                                isOurWindow = true;
                                break;
                            }
                            parent = TreeWalker.ControlViewWalker.GetParent(parent);
                        }
                    }
                    catch { /* Ignore errors walking tree */ }
                }

                if (isOurWindow)
                {
                    Logger.Log($"Focus changed to LocalScribe element '{name}' - preserving TextPattern");
                    return;
                }

                StopPolling(); // Stop polling previous element

                _lastFocusedElement = element;
                _lastClassName = className;

                // Try to get process name for application-specific handling
                try
                {
                    int processId = element.Current.ProcessId;
                    var process = Process.GetProcessById(processId);
                    _lastProcessName = process.ProcessName;
                }
                catch
                {
                    _lastProcessName = string.Empty;
                }

                // Determine if we should process this element
                bool shouldProcess = false;
                if (controlType == ControlType.Edit)
                {
                    // Always process Edit controls (text inputs, text areas, etc.)
                    shouldProcess = true;
                    Logger.Log("Focus: Edit control - will process");
                }
                else if (controlType == ControlType.Document)
                {
                    // For Document controls, check if editable (skip read-only web pages)
                    shouldProcess = IsDocumentEditable(element);
                    Logger.Log($"Focus: Document control - editable={shouldProcess}");
                }

                if (shouldProcess)
                {
                    // Initial read
                    var (text, bounds, caretBounds) = ReadTextAndBounds(element);
                    Logger.Log($"Initial Read: {text.Length} chars. Bounds: {bounds} Caret: {caretBounds}");

                    if (text != _lastObservedText || bounds != _lastBounds || caretBounds != _lastCaretBounds)
                    {
                        _lastObservedText = text;
                        _lastBounds = bounds;
                        _lastCaretBounds = caretBounds;
                        TextChanged?.Invoke(this, (text, bounds, caretBounds));
                    }

                    // Start polling for changes
                    StartPolling(element);
                }
            }
            catch (ElementNotAvailableException)
            {
                // Element might be gone
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing focus change: {ex.Message}");
            }
        }

        private void StartPolling(AutomationElement element)
        {
            _pollingCts = new CancellationTokenSource();
            var token = _pollingCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(_pollingIntervalMs, token); // Configurable polling interval
                    if (token.IsCancellationRequested) break;

                    try
                    {
                        var (currentText, currentBounds, currentCaretBounds) = ReadTextAndBounds(element);
                        // Simple check: only fire if text length changed or content changed significantly
                        if (currentText != _lastObservedText || currentBounds != _lastBounds || currentCaretBounds != _lastCaretBounds)
                        {
                            _lastObservedText = currentText;
                            _lastBounds = currentBounds;
                            _lastCaretBounds = currentCaretBounds;
                            // Logger.Log($"Text/Bounds Changed detected."); // Too noisy?
                            TextChanged?.Invoke(this, (currentText, currentBounds, currentCaretBounds));
                        }
                    }
                    catch
                    {
                        // If reading fails (element closed?), stop polling
                        break;
                    }
                }
            }, token);
        }
        private void StopPolling()
        {
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;
            _lastObservedText = string.Empty; // Reset for new focus
            _lastBounds = Rect.Empty;
            _lastCaretBounds = Rect.Empty;
            _lastTextPattern = null; // Clear cached pattern
        }

        private (string Text, Rect ElementBounds, Rect CaretBounds) ReadTextAndBounds(AutomationElement element)
        {
            string text = string.Empty;
            Rect bounds = Rect.Empty;
            Rect caretBounds = Rect.Empty;

            try
            {
                bounds = element.Current.BoundingRectangle;

                object patternObj;
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out patternObj))
                {
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
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to read text/bounds: {ex.Message}");
            }
            return (text, bounds, caretBounds);
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
