using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Attached behaviors for WPF TextBox to provide smooth industrial UX:
/// - SelectAllOnFocus: Automatically highlights all text on click or tab so typing replaces the existing value immediately.
/// - CommitOnEnter: Pressing Enter commits the binding and moves focus away.
/// </summary>
public static class TextBoxBehaviors
{
    public static readonly DependencyProperty SelectAllOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "SelectAllOnFocus",
            typeof(bool),
            typeof(TextBoxBehaviors),
            new UIPropertyMetadata(false, OnSelectAllOnFocusChanged));

    public static bool GetSelectAllOnFocus(DependencyObject obj) => (bool)obj.GetValue(SelectAllOnFocusProperty);
    public static void SetSelectAllOnFocus(DependencyObject obj, bool value) => obj.SetValue(SelectAllOnFocusProperty, value);

    public static readonly DependencyProperty CommitOnLostFocusCommandProperty =
        DependencyProperty.RegisterAttached(
            "CommitOnLostFocusCommand",
            typeof(ICommand),
            typeof(TextBoxBehaviors),
            new UIPropertyMetadata(null, OnCommitOnLostFocusCommandChanged));

    public static ICommand? GetCommitOnLostFocusCommand(DependencyObject obj) => (ICommand?)obj.GetValue(CommitOnLostFocusCommandProperty);
    public static void SetCommitOnLostFocusCommand(DependencyObject obj, ICommand? value) => obj.SetValue(CommitOnLostFocusCommandProperty, value);

    private static void OnCommitOnLostFocusCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if (e.OldValue is ICommand)
            {
                textBox.LostFocus -= OnTextBoxLostFocus;
            }
            if (e.NewValue is ICommand)
            {
                textBox.LostFocus += OnTextBoxLostFocus;
            }
        }
    }

    private static void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var cmd = GetCommitOnLostFocusCommand(textBox);
            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
            }
        }
    }

    private static void OnSelectAllOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox textBox)
        {
            if ((bool)e.NewValue)
            {
                textBox.GotKeyboardFocus += OnGotKeyboardFocus;
                textBox.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                textBox.KeyDown += OnKeyDown;
            }
            else
            {
                textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
                textBox.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                textBox.KeyDown -= OnKeyDown;
            }
        }
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.SelectAll();
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            textBox.Focus();
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            if (e.Key == Key.Enter)
            {
                // Commit binding
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                var cmd = GetCommitOnLostFocusCommand(textBox);
                if (cmd != null && cmd.CanExecute(null))
                {
                    cmd.Execute(null);
                }

                // Move focus to clear caret and trigger formatting
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Cancel edit - restore original binding value
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateTarget();
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }
    }
}
