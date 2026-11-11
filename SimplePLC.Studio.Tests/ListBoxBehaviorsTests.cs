using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimplePLC.Studio.Services;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="ListBoxBehaviors"/> attached property and auto-synchronization logic.
/// Verifies the industrial Visual Truth axiom: active focus or click on any child control
/// immediately synchronizes row selection and clears stale selections.
/// </summary>
public class ListBoxBehaviorsTests
{
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    [Fact]
    public void ListBoxBehaviors_SetAndGetAutoSelectOnFocus_TogglesFlag()
    {
        RunOnStaThread(() =>
        {
            var listBox = new ListBox();
            Assert.False(ListBoxBehaviors.GetAutoSelectOnFocus(listBox));

            ListBoxBehaviors.SetAutoSelectOnFocus(listBox, true);
            Assert.True(ListBoxBehaviors.GetAutoSelectOnFocus(listBox));

            ListBoxBehaviors.SetAutoSelectOnFocus(listBox, false);
            Assert.False(ListBoxBehaviors.GetAutoSelectOnFocus(listBox));
        });
    }

    [Fact]
    public void ListBoxBehaviors_WhenChildControlReceivesFocus_AutoSelectsParentListBoxItem()
    {
        RunOnStaThread(() =>
        {
            var listBox = new ListBox();
            ListBoxBehaviors.SetAutoSelectOnFocus(listBox, true);

            var item0 = new ListBoxItem();
            var textBox0 = new TextBox { Text = "Row 0" };
            item0.Content = textBox0;

            var item1 = new ListBoxItem();
            var textBox1 = new TextBox { Text = "Row 1" };
            item1.Content = textBox1;

            listBox.Items.Add(item0);
            listBox.Items.Add(item1);

            // Simulate Row 1 being previously selected (like Row 7 at the bottom)
            listBox.SelectedItem = item1;
            Assert.True(item1.IsSelected);
            Assert.False(item0.IsSelected);

            // Simulate user clicking / focusing into Row 0's TextBox
            var focusArgs = new KeyboardFocusChangedEventArgs(
                Keyboard.PrimaryDevice,
                0,
                textBox1,
                textBox0)
            {
                RoutedEvent = UIElement.GotKeyboardFocusEvent,
                Source = textBox0
            };

            listBox.RaiseEvent(focusArgs);

            // Row 0 must now be selected, and Row 1 unselected
            Assert.True(item0.IsSelected, "Row 0 ListBoxItem must be selected when its child TextBox receives focus.");
            Assert.False(item1.IsSelected, "Row 1 ListBoxItem must be unselected.");
            Assert.Same(item0, listBox.SelectedItem);
        });
    }

    [Fact]
    public void ListBoxBehaviors_WhenDisabled_ChildControlFocusDoesNotChangeSelection()
    {
        RunOnStaThread(() =>
        {
            var listBox = new ListBox();
            // Disabled by default
            ListBoxBehaviors.SetAutoSelectOnFocus(listBox, false);

            var item0 = new ListBoxItem();
            var textBox0 = new TextBox { Text = "Row 0" };
            item0.Content = textBox0;

            var item1 = new ListBoxItem();
            var textBox1 = new TextBox { Text = "Row 1" };
            item1.Content = textBox1;

            listBox.Items.Add(item0);
            listBox.Items.Add(item1);

            // Row 1 selected
            listBox.SelectedItem = item1;

            // Focus on Row 0's TextBox
            var focusArgs = new KeyboardFocusChangedEventArgs(
                Keyboard.PrimaryDevice,
                0,
                textBox1,
                textBox0)
            {
                RoutedEvent = UIElement.GotKeyboardFocusEvent,
                Source = textBox0
            };

            listBox.RaiseEvent(focusArgs);

            // Row 1 remains selected because behavior is disabled
            Assert.False(item0.IsSelected);
            Assert.True(item1.IsSelected);
            Assert.Same(item1, listBox.SelectedItem);
        });
    }
}
