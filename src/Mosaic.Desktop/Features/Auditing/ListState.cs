namespace Mosaic.Desktop.Features.Auditing;

// what the audit screen shows: its rows, or a message in their place
public enum ListState
{
    // a page is on its way and no rows are shown
    Loading,

    // the rows of a page
    Ready,

    // no rows, and the server says the log holds nothing at all
    Empty,

    // no rows for the filters, though the log holds some
    NoMatch,

    // the load did not work, and the reason stands in place of the rows
    Failed
}
