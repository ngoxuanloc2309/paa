using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class LogicEditorView : UserControl
{
    private GridLength _lastDrawerHeight = new GridLength(240);

    public LogicEditorView()
    {
        InitializeComponent();
        Nodify.NodifyEditor.EnableSnappingCorrection = false;
        Nodify.NodifyEditor.EnableDraggingContainersOptimizations = true;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is LogicEditorViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            oldVm.RequestCenterOnNode = null;
        }
        if (e.NewValue is LogicEditorViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            newVm.RequestCenterOnNode = CenterOnNode;
            UpdateDrawerState(newVm.IsSimulationMode);
        }
    }

    private void CenterOnNode(GraphNodeViewModel node)
    {
        if (Editor == null || node == null) return;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            try
            {
                // Tọa độ mục tiêu của Node trên không gian logic
                var nodeLoc = node.Location;
                double nodeWidth = 180;
                double nodeHeight = 86;

                // Tính toán để căn tâm Node vào giữa màn hình NodifyEditor
                double vpWidth = Editor.ActualWidth > 0 ? Editor.ActualWidth : 800;
                double vpHeight = Editor.ActualHeight > 0 ? Editor.ActualHeight : 600;
                double zoom = Editor.ViewportZoom > 0 ? Editor.ViewportZoom : 1.0;

                double targetX = (nodeLoc.X + nodeWidth / 2.0) - (vpWidth / (2.0 * zoom));
                double targetY = (nodeLoc.Y + nodeHeight / 2.0) - (vpHeight / (2.0 * zoom));

                Editor.ViewportLocation = new Point(targetX, targetY);
            }
            catch { }
        }));
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LogicEditorViewModel.IsSimulationMode) && sender is LogicEditorViewModel vm)
        {
            UpdateDrawerState(vm.IsSimulationMode);
        }
    }

    private void UpdateDrawerState(bool isSimMode)
    {
        if (SimDrawerRow == null) return;

        if (isSimMode)
        {
            SimDrawerRow.MinHeight = 140;
            SimDrawerRow.MaxHeight = 600;
            SimDrawerRow.Height = _lastDrawerHeight.Value >= 140 ? _lastDrawerHeight : new GridLength(240);
        }
        else
        {
            if (SimDrawerRow.ActualHeight >= 140)
            {
                _lastDrawerHeight = new GridLength(SimDrawerRow.ActualHeight);
            }
            else if (SimDrawerRow.Height.Value >= 140)
            {
                _lastDrawerHeight = SimDrawerRow.Height;
            }
            SimDrawerRow.MinHeight = 0;
            SimDrawerRow.Height = new GridLength(0);
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Không chặn các phím tắt nếu người dùng đang nhập liệu trong TextBox
        if (Keyboard.FocusedElement is TextBoxBase)
        {
            return;
        }

        if (DataContext is LogicEditorViewModel vm)
        {
            var mods = Keyboard.Modifiers;

            if (mods == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        vm.Undo();
                        e.Handled = true;
                        return;
                    case Key.Y:
                        vm.Redo();
                        e.Handled = true;
                        return;
                    case Key.C:
                        vm.Copy();
                        e.Handled = true;
                        return;
                    case Key.X:
                        vm.Cut();
                        e.Handled = true;
                        return;
                    case Key.V:
                        vm.Paste();
                        e.Handled = true;
                        return;
                    case Key.D:
                        vm.Duplicate();
                        e.Handled = true;
                        return;
                    case Key.A:
                        vm.SelectAll();
                        e.Handled = true;
                        return;
                    case Key.S:
                        vm.SaveAndCompile();
                        e.Handled = true;
                        return;
                }
            }
            else if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                switch (e.Key)
                {
                    case Key.Z:
                        vm.Redo();
                        e.Handled = true;
                        return;
                    case Key.Up:
                        vm.AlignTop();
                        e.Handled = true;
                        return;
                    case Key.Down:
                        vm.AlignBottom();
                        e.Handled = true;
                        return;
                    case Key.Left:
                        vm.AlignLeft();
                        e.Handled = true;
                        return;
                    case Key.Right:
                        vm.AlignRight();
                        e.Handled = true;
                        return;
                    case Key.M:
                        vm.AlignMiddle();
                        e.Handled = true;
                        return;
                    case Key.C:
                        vm.AlignCenter();
                        e.Handled = true;
                        return;
                    case Key.H:
                        vm.DistributeNodesHorizontally();
                        e.Handled = true;
                        return;
                    case Key.V:
                        vm.DistributeNodesVertically();
                        e.Handled = true;
                        return;
                    case Key.G:
                        vm.SnapAllNodesToGrid();
                        e.Handled = true;
                        return;
                }
            }
            else if (mods == ModifierKeys.Alt)
            {
                var sysKey = e.Key == Key.System ? e.SystemKey : e.Key;
                switch (sysKey)
                {
                    case Key.Up:
                        vm.AlignTop();
                        e.Handled = true;
                        return;
                    case Key.Down:
                        vm.AlignBottom();
                        e.Handled = true;
                        return;
                    case Key.Left:
                        vm.AlignLeft();
                        e.Handled = true;
                        return;
                    case Key.Right:
                        vm.AlignRight();
                        e.Handled = true;
                        return;
                    case Key.M:
                        vm.AlignMiddle();
                        e.Handled = true;
                        return;
                    case Key.C:
                        vm.AlignCenter();
                        e.Handled = true;
                        return;
                    case Key.G:
                        vm.SnapAllNodesToGrid();
                        e.Handled = true;
                        return;
                }
            }
            else if (mods == (ModifierKeys.Alt | ModifierKeys.Shift))
            {
                var sysKey = e.Key == Key.System ? e.SystemKey : e.Key;
                switch (sysKey)
                {
                    case Key.H:
                        vm.DistributeNodesHorizontally();
                        e.Handled = true;
                        return;
                    case Key.V:
                        vm.DistributeNodesVertically();
                        e.Handled = true;
                        return;
                }
            }
            else if (e.Key == Key.Delete)
            {
                if (vm.SelectedNode != null || vm.SelectedNodes.Count > 0 || vm.Nodes.Any(n => n.IsSelected) || vm.Connections.Any(c => c.IsSelected))
                {
                    vm.DeleteSelectedNode();
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void Connection_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ConnectionViewModel conn && DataContext is LogicEditorViewModel vm)
        {
            bool isCtrlOrShift = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;

            if (e.ChangedButton == MouseButton.Left)
            {
                if (!isCtrlOrShift)
                {
                    vm.DeselectAllNodes();
                    vm.DeselectAllConnections();
                }
                conn.IsSelected = !isCtrlOrShift || !conn.IsSelected;
                e.Handled = true;
                fe.Focus();
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                if (!conn.IsSelected)
                {
                    if (!isCtrlOrShift)
                    {
                        vm.DeselectAllNodes();
                        vm.DeselectAllConnections();
                    }
                    conn.IsSelected = true;
                }
                // Để mở ContextMenu trên dây
            }
        }
    }

    private void NodifyEditor_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is LogicEditorViewModel vm)
        {
            bool isCtrlOrShift = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
            if (!isCtrlOrShift)
            {
                var hit = e.OriginalSource as DependencyObject;
                bool isOverConnectionOrNode = false;
                while (hit != null && hit != sender)
                {
                    if (hit is FrameworkElement fe && (fe.DataContext is ConnectionViewModel || fe.DataContext is GraphNodeViewModel))
                    {
                        isOverConnectionOrNode = true;
                        break;
                    }
                    hit = System.Windows.Media.VisualTreeHelper.GetParent(hit);
                }

                if (!isOverConnectionOrNode)
                {
                    vm.DeselectAllConnections();
                }
            }
        }
    }

    private void ToolboxItem_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe && fe.Tag is string tag)
        {
            DragDrop.DoDragDrop(fe, tag, DragDropEffects.Copy);
        }
    }

    private void ToolboxSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            string query = tb.Text?.Trim() ?? string.Empty;
            if (BtnClearToolboxSearch != null)
            {
                BtnClearToolboxSearch.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;
            }
            if (ToolboxWatermarkText != null)
            {
                ToolboxWatermarkText.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            }
            ApplyToolboxFilter(query);
        }
    }

    private void BtnClearToolboxSearch_Click(object sender, RoutedEventArgs e)
    {
        if (ToolboxSearchBox != null)
        {
            ToolboxSearchBox.Text = string.Empty;
        }
    }

    private void ApplyToolboxFilter(string query)
    {
        if (ToolboxItemsHost == null) return;

        bool hasQuery = !string.IsNullOrWhiteSpace(query);

        foreach (var child in ToolboxItemsHost.Children)
        {
            if (child is Expander expander && expander.Content is Panel panel)
            {
                int visibleInExpander = 0;
                foreach (var item in panel.Children)
                {
                    if (item is FrameworkElement fe)
                    {
                        if (!hasQuery)
                        {
                            fe.Visibility = Visibility.Visible;
                            visibleInExpander++;
                        }
                        else
                        {
                            string tag = fe.Tag as string ?? "";
                            string tooltip = fe.ToolTip as string ?? "";
                            bool match = tag.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                         tooltip.Contains(query, StringComparison.OrdinalIgnoreCase);

                            if (!match)
                            {
                                foreach (var tb in FindVisualChildren<TextBlock>(fe))
                                {
                                    if (tb.Text != null && tb.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                                    {
                                        match = true;
                                        break;
                                    }
                                }
                            }

                            fe.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                            if (match) visibleInExpander++;
                        }
                    }
                }

                if (!hasQuery)
                {
                    expander.Visibility = Visibility.Visible;
                }
                else
                {
                    expander.Visibility = visibleInExpander > 0 ? Visibility.Visible : Visibility.Collapsed;
                    if (visibleInExpander > 0)
                    {
                        expander.IsExpanded = true;
                    }
                }
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
            {
                yield return t;
            }
            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }

    private Point _mouseDownPos;
    private SimplePLC.Studio.Services.GraphSnapshot? _preDragSnapshot;

    private void NodifyEditor_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is LogicEditorViewModel vm)
        {
            if (e.Data.GetDataPresent(typeof(string)))
            {
                string? nodeType = e.Data.GetData(typeof(string)) as string;
                if (!string.IsNullOrEmpty(nodeType))
                {
                    Point position;
                    if (sender is Nodify.NodifyEditor editor)
                    {
                        // Chuyển đổi tọa độ thả chuột chính xác theo không gian của NodifyEditor (đã tính độ trượt Panning và Tỉ lệ Zoom)
                        position = editor.GetLocationInsideEditor(e);
                        uint cellSize = editor.GridCellSize;
                        if (cellSize > 0)
                        {
                            position = new Point(
                                Math.Round(position.X / cellSize) * cellSize,
                                Math.Round(position.Y / cellSize) * cellSize
                            );
                        }
                    }
                    else
                    {
                        position = e.GetPosition((IInputElement)sender);
                    }

                    vm.AddNode(nodeType, position);
                }
            }
        }
    }

    private void ItemContainer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GraphNodeViewModel node && DataContext is LogicEditorViewModel vm)
        {
            _mouseDownPos = e.GetPosition(this);
            _preDragSnapshot = vm.CaptureSnapshot("Move Nodes");

            // Khi click vào khối mà không nhấn giữ Ctrl/Shift: bỏ chọn tất cả các dây
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0)
            {
                vm.DeselectAllConnections();
            }

            // Nhấp chuột phải vào khối đang nằm trong nhóm quét chọn: giữ nguyên toàn bộ nhóm để thao tác context menu
            if (e.ChangedButton == MouseButton.Right && node.IsSelected)
            {
                vm.SelectedNode = node;
                fe.Focus();
                return;
            }

            // Nếu nhấn giữ Ctrl hoặc Shift: cho phép cơ chế chọn đa khối của Nodify hoạt động
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
            {
                vm.SelectedNode = node;
                fe.Focus();
                return;
            }

            // Click chuột trái vào một khối chưa chọn: CHỌN ĐỘC QUYỀN khối này, hủy chọn tất cả các khối khác
            if (!node.IsSelected)
            {
                vm.SelectExclusive(node);
            }
            else
            {
                vm.SelectedNode = node;
            }

            fe.Focus();
        }
    }

    private void ItemContainer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GraphNodeViewModel node && DataContext is LogicEditorViewModel vm)
        {
            if (e.ChangedButton == MouseButton.Left && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0)
            {
                var currentPos = e.GetPosition(this);
                // Nếu chỉ click tại chỗ (không kéo rê khối đi quá 4px) mà trước đó đang có nhiều khối được chọn:
                if (Math.Abs(currentPos.X - _mouseDownPos.X) < 4 && Math.Abs(currentPos.Y - _mouseDownPos.Y) < 4)
                {
                    _preDragSnapshot = null;
                    if (vm.SelectedNodes.Count > 1 || vm.Nodes.Count(n => n.IsSelected) > 1)
                    {
                        vm.SelectExclusive(node);
                    }
                }
                else
                {
                    // Người dùng vừa kết thúc thao tác kéo rê khối:
                    if (_preDragSnapshot != null)
                    {
                        vm.PushSnapshot(_preDragSnapshot);
                        _preDragSnapshot = null;
                    }

                    // Tự động kích hoạt Bắt dính Nam châm Thông minh (Smart Magnetic Alignment):
                    // Chỉ áp dụng khi kéo 1 khối đơn lẻ; nếu kéo đa khối thì giữ nguyên tương quan vị trí giữa chúng
                    if (vm.SelectedNodes.Count <= 1)
                    {
                        vm.ApplyMagneticAlignment(node);
                    }
                    else
                    {
                        vm.CompileAndSaveRules(true);
                    }
                }
            }
        }
    }
}
