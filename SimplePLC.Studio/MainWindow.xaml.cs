using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio;

public partial class MainWindow : Window
{
    private double _savedAiChatWidth = 380;

    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;

        UpdateAiChatColumnWidth(vm.AiChatVM.IsPanelOpen);

        vm.AiChatVM.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AiChatViewModel.IsPanelOpen))
            {
                UpdateAiChatColumnWidth(vm.AiChatVM.IsPanelOpen);
            }
        };
    }

    private void UpdateAiChatColumnWidth(bool isOpen)
    {
        if (isOpen)
        {
            AiChatColumn.Width = new GridLength(_savedAiChatWidth > 200 ? _savedAiChatWidth : 380, GridUnitType.Pixel);
            AiChatColumn.MaxWidth = double.PositiveInfinity;
        }
        else
        {
            if (AiChatColumn.ActualWidth > 200)
            {
                _savedAiChatWidth = AiChatColumn.ActualWidth;
            }
            AiChatColumn.Width = new GridLength(0);
            AiChatColumn.MaxWidth = 0;
        }
    }

    private void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.AiChatVM.IsPanelOpen)
        {
            // Dragging left (e.HorizontalChange < 0) increases width of right column
            // Dragging right (e.HorizontalChange > 0) decreases width of right column
            double currentWidth = AiChatColumn.ActualWidth > 0 ? AiChatColumn.ActualWidth : (_savedAiChatWidth > 200 ? _savedAiChatWidth : 380);
            double newWidth = currentWidth - e.HorizontalChange;
            if (newWidth < 260) newWidth = 260;
            double maxWidth = Math.Max(300, ActualWidth - 300);
            if (newWidth > maxWidth) newWidth = maxWidth;

            _savedAiChatWidth = newWidth;
            AiChatColumn.Width = new GridLength(newWidth, GridUnitType.Pixel);
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (vm.PromptSaveIfDirty() == false)
            {
                e.Cancel = true;
            }
        }
    }
}
