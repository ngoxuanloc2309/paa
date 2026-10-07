using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class AiChatView : UserControl
{
    public AiChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AiChatViewModel oldVm)
        {
            oldVm.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        }

        if (e.NewValue is AiChatViewModel newVm)
        {
            newVm.Messages.CollectionChanged += OnMessagesCollectionChanged;
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.InvokeAsync(() =>
            {
                ChatScrollViewer.ScrollToEnd();
            });
        }
    }

    private void PromptTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            if (DataContext is AiChatViewModel vm && vm.SendMessageCommand.CanExecute(null))
            {
                vm.SendMessageCommand.Execute(null);
            }
        }
    }
}
