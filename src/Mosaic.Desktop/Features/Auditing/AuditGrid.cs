using Avalonia.Controls;

namespace Mosaic.Desktop.Features.Auditing;

// the header and every row are one of these, so the columns are written once and always line up
public sealed class AuditGrid : Grid
{
    public AuditGrid()
    {
        ColumnDefinitions = new ColumnDefinitions("2*,2*,3*,2*,2*");
        ColumnDefinitions[0].MinWidth = 170;
    }
}
