using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
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

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AiChatViewModel oldVm)
        {
            oldVm.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is AiChatViewModel newVm)
        {
            newVm.Messages.CollectionChanged += OnMessagesCollectionChanged;
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AiChatViewModel.IsBusy))
        {
            if (DataContext is AiChatViewModel vm && !vm.IsBusy)
            {
                ReturnFocusToPrompt();
            }
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
            if (DataContext is AiChatViewModel vm && !string.IsNullOrWhiteSpace(vm.InputText) && vm.SendMessageCommand.CanExecute(null))
            {
                e.Handled = true;
                vm.SendMessageCommand.Execute(null);
                ReturnFocusToPrompt();
            }
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        ReturnFocusToPrompt();
    }

    public void ReturnFocusToPrompt()
    {
        Dispatcher.InvokeAsync(() =>
        {
            PromptTextBox.Focus();
            Keyboard.Focus(PromptTextBox);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
}
