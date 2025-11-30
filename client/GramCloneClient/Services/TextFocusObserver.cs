using System;
using System.Diagnostics;
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
        private CancellationTokenSource? _pollingCts;
        private string _lastObservedText = string.Empty;
        private Rect _lastBounds = Rect.Empty;
        private Rect _lastCaretBounds = Rect.Empty;

        public event EventHandler<(string Text, Rect ElementBounds, Rect CaretBounds)>? TextChanged;

        public TextFocusObserver()
        {
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

                StopPolling(); // Stop polling previous element

                _lastFocusedElement = element;

                // Basic filter: We only care about Edit or Document controls
                var controlType = element.Current.ControlType;
                string className = element.Current.ClassName;
                string name = element.Current.Name;
                
                Logger.Log($"Focus: Name='{name}', Type='{controlType.LocalizedControlType}', Class='{className}'");

                if (controlType == ControlType.Edit || controlType == ControlType.Document)
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
                    await Task.Delay(1000, token); // Poll every 1 second
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

        public void Dispose()
        {
            Stop();
        }
    }
}
