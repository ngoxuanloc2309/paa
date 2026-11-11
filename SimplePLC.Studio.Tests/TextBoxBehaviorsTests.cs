using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Services;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="TextBoxBehaviors"/> attached properties and commands.
/// </summary>
public class TextBoxBehaviorsTests
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
    public void TextBoxBehaviors_SetAndGetCommitOnLostFocusCommand_PreservesCommandInstance()
    {
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();
            bool executed = false;
            var command = new RelayCommand(() => executed = true);

            TextBoxBehaviors.SetCommitOnLostFocusCommand(textBox, command);
            var retrievedCommand = TextBoxBehaviors.GetCommitOnLostFocusCommand(textBox);

            Assert.Same(command, retrievedCommand);
            retrievedCommand?.Execute(null);
            Assert.True(executed);
        });
    }

    [Fact]
    public void TextBoxBehaviors_SetAndGetSelectAllOnFocus_TogglesFlag()
    {
        RunOnStaThread(() =>
        {
            var textBox = new TextBox();

            Assert.False(TextBoxBehaviors.GetSelectAllOnFocus(textBox));

            TextBoxBehaviors.SetSelectAllOnFocus(textBox, true);
            Assert.True(TextBoxBehaviors.GetSelectAllOnFocus(textBox));

            TextBoxBehaviors.SetSelectAllOnFocus(textBox, false);
            Assert.False(TextBoxBehaviors.GetSelectAllOnFocus(textBox));
        });
    }
}
