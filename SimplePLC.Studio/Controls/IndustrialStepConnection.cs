namespace SimplePLC.Studio.Controls;

using System;
using System.Windows;
using System.Windows.Media;

/// <summary>
/// Dây kết nối công nghiệp chuẩn kỹ thuật (Industrial Step Connection):
/// Kế thừa Nodify.StepConnection, bổ sung cơ chế khử gập subpixel (Snap Threshold 3.0px):
/// - Khi hai chân cắm gần như cùng độ cao (chênh lệch Y <= 3.0px do làm tròn layout rendering của WPF),
///   dây sẽ được vẽ thành một đường nằm ngang thẳng tắp 100%, loại bỏ hoàn toàn nếp gập 1px zíc-zắc.
/// - Khi chênh lệch Y > 3.0px, dây tự động chuyển sang đường bậc thang vuông góc 90 độ chuẩn PLC Ladder.
/// </summary>
public class IndustrialStepConnection : Nodify.StepConnection
{
    protected override Geometry DefiningGeometry
    {
        get
        {
            // Bù trừ sai số layout rounding của WPF rendering engine
            if (Math.Abs(Source.Y - Target.Y) <= 3.0)
            {
                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(Source, isFilled: false, isClosed: false);
                    ctx.LineTo(new Point(Target.X, Source.Y), isStroked: true, isSmoothJoin: false);
                }
                geom.Freeze();
                return geom;
            }

            return base.DefiningGeometry;
        }
    }
}
