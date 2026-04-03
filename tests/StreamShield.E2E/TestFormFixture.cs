using System;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace StreamShield.E2E;

/// <summary>
/// Creates a real WinForms window with labeled input fields that UIA can inspect.
/// The fixture runs on an STA thread (WinForms requirement) and exposes
/// AutomationElements for each field so tests can drive the detection pipeline.
/// </summary>
public sealed class TestFormFixture : IDisposable
{
    private readonly Thread _uiThread;
    private readonly ManualResetEventSlim _ready = new();
    private Form? _form;
    private Panel? _scrollPanel;

    public IntPtr FormHandle { get; private set; }
    public IntPtr PasswordFieldHandle { get; private set; }
    public IntPtr CardNumberFieldHandle { get; private set; }
    public IntPtr HebrewIdFieldHandle { get; private set; }
    public IntPtr SsnFieldHandle { get; private set; }
    public IntPtr EmailFieldHandle { get; private set; }
    public IntPtr ScrollPasswordFieldHandle { get; private set; }
    public IntPtr ScrollCardFieldHandle { get; private set; }

    public TestFormFixture()
    {
        _uiThread = new Thread(RunForm) { IsBackground = true };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait(TimeSpan.FromSeconds(10));
    }

    private void RunForm()
    {
        _form = new Form
        {
            Text = "StreamShield E2E Test",
            Size = new Size(500, 400),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(100, 100),
            TopMost = true
        };

        int y = 20;

        // Password field (UIA IsPassword = true)
        var pwLabel = new Label { Text = "Password:", Location = new Point(20, y), AutoSize = true };
        var pwField = new TextBox
        {
            Location = new Point(200, y),
            Size = new Size(250, 25),
            UseSystemPasswordChar = true
        };
        _form.Controls.AddRange([pwLabel, pwField]);
        y += 40;

        // Card Number field (keyword match)
        var ccLabel = new Label { Text = "Card Number:", Location = new Point(20, y), AutoSize = true };
        var ccField = new TextBox
        {
            Location = new Point(200, y),
            Size = new Size(250, 25),
            AccessibleName = "Card Number"
        };
        _form.Controls.AddRange([ccLabel, ccField]);
        y += 40;

        // Hebrew ID field (ת.ז.)
        var hebrewLabel = new Label { Text = "\u05ea.\u05d6.:", Location = new Point(20, y), AutoSize = true };
        var hebrewField = new TextBox
        {
            Location = new Point(200, y),
            Size = new Size(250, 25),
            AccessibleName = "\u05ea.\u05d6"
        };
        _form.Controls.AddRange([hebrewLabel, hebrewField]);
        y += 40;

        // SSN field
        var ssnLabel = new Label { Text = "SSN:", Location = new Point(20, y), AutoSize = true };
        var ssnField = new TextBox
        {
            Location = new Point(200, y),
            Size = new Size(250, 25),
            AccessibleName = "SSN"
        };
        _form.Controls.AddRange([ssnLabel, ssnField]);
        y += 40;

        // Email field (should NOT be detected)
        var emailLabel = new Label { Text = "Email:", Location = new Point(20, y), AutoSize = true };
        var emailField = new TextBox
        {
            Location = new Point(200, y),
            Size = new Size(250, 25),
            AccessibleName = "Email"
        };
        _form.Controls.AddRange([emailLabel, emailField]);
        y += 40;

        // Scrollable panel with fields for scroll tracking tests
        var scrollPanel = new Panel
        {
            Location = new Point(0, y),
            Size = new Size(480, 80),
            AutoScroll = true
        };

        int sy = 10;
        var spwLabel = new Label { Text = "Password:", Location = new Point(20, sy), AutoSize = true };
        var spwField = new TextBox
        {
            Location = new Point(200, sy),
            Size = new Size(250, 25),
            UseSystemPasswordChar = true,
            AccessibleName = "ScrollPassword"
        };
        scrollPanel.Controls.AddRange([spwLabel, spwField]);
        sy += 40;

        var sccLabel = new Label { Text = "Card Number:", Location = new Point(20, sy), AutoSize = true };
        var sccField = new TextBox
        {
            Location = new Point(200, sy),
            Size = new Size(250, 25),
            AccessibleName = "Card Number"
        };
        scrollPanel.Controls.AddRange([sccLabel, sccField]);
        sy += 40;

        // Spacer to force scrollability
        scrollPanel.Controls.Add(new Panel { Location = new Point(0, sy), Size = new Size(10, 300) });
        _scrollPanel = scrollPanel;
        _form.Controls.Add(scrollPanel);

        _form.Size = new Size(500, y + 120);

        _form.Shown += (_, _) =>
        {
            FormHandle = _form.Handle;
            PasswordFieldHandle = pwField.Handle;
            CardNumberFieldHandle = ccField.Handle;
            HebrewIdFieldHandle = hebrewField.Handle;
            SsnFieldHandle = ssnField.Handle;
            EmailFieldHandle = emailField.Handle;
            ScrollPasswordFieldHandle = spwField.Handle;
            ScrollCardFieldHandle = sccField.Handle;
            _ready.Set();
        };

        Application.Run(_form);
    }

    /// <summary>Focus a field by its handle using UIA SetFocus (works even when test runner has focus).</summary>
    public void FocusField(IntPtr handle)
    {
        // Bring our form to the foreground first
        NativeMethods.SetForegroundWindow(FormHandle);
        Thread.Sleep(50);

        // Use UIA SetFocus which properly activates the window and focuses the element
        var element = AutomationElement.FromHandle(handle);
        element.SetFocus();
        Thread.Sleep(150); // Let UIA catch up
    }

    /// <summary>Scroll the test panel by the given delta (positive = down).</summary>
    public void ScrollPanel(int deltaY)
    {
        if (_form is null || _form.IsDisposed || _scrollPanel is null) return;
        _form.Invoke(() =>
        {
            var pos = _scrollPanel.AutoScrollPosition;
            _scrollPanel.AutoScrollPosition = new Point(0, -pos.Y + deltaY);
        });
        Thread.Sleep(100);
    }

    public void Dispose()
    {
        if (_form is not null && !_form.IsDisposed)
        {
            _form.Invoke(() => _form.Close());
        }
        _ready.Dispose();
    }
}
