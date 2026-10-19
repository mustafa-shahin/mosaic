using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Mosaic.Desktop;

// the real App, so a view under test gets the templates and styles the window gets
[assembly: AvaloniaTestApplication(typeof(App))]

// every test on Avalonia's thread: once a view test has started the App, code that touches it from another thread throws
[assembly: AvaloniaTestFramework]
