using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TowEstimator;

public sealed class MainForm : Form
{
    private readonly Storage _storage = new();
    private readonly HttpClient _httpClient = new();
    private readonly GeoapifyClient _geoClient;
    private readonly EstimatorPreferences _prefs;
    private readonly List<LogEntry> _logs;

    private readonly TextBox _deadheadInput = new();
    private readonly TextBox _pickupInput = new();
    private readonly TextBox _dropoffInput = new();
    private readonly CheckBox _useDefaultYardToggle = new();
    private readonly NumericUpDown _baseFeeInput = new();
    private readonly NumericUpDown _rateInput = new();
    private readonly NumericUpDown _discountPercentInput = new();
    private readonly NumericUpDown _discountAmountInput = new();
    private readonly TextBox _discountReasonInput = new();
    private readonly TextBox _customerNameInput = new();
    private readonly TextBox _customerPhonePrimaryInput = new();
    private readonly TextBox _customerPhoneSecondaryInput = new();
    private readonly TextBox _resultBox = new();
    private readonly ListView _logList = new();
    private readonly Label _logEmpty = new();
    private readonly DateTimePicker _logDate = new();
    private readonly TextBox _logSearch = new();
    private readonly Label _heroYard = new();
    private readonly Label _yardInfoLabel = new();
    private readonly ToolTip _toolTip = new();

    private GeoPoint? _deadheadPoint;
    private GeoPoint? _pickupPoint;
    private GeoPoint? _dropoffPoint;
    private LogEntry? _lastEstimate;

    private readonly Dictionary<TextBox, ListBox> _autocompleteLists = new();
    private readonly Dictionary<TextBox, CancellationTokenSource> _autocompleteTokens = new();

    public MainForm()
    {
        _geoClient = new GeoapifyClient(_httpClient);
        _prefs = _storage.LoadPreferences();
        _logs = _storage.LoadLogs();

        Text = "Southern Pride Towing — Price Estimator";
        MinimumSize = new Size(1100, 760);
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 2,
            ColumnCount = 1,
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(mainPanel);

        mainPanel.Controls.Add(BuildHero());
        mainPanel.Controls.Add(BuildTabs());

        LoadPreferencesIntoUi();
        RenderLogs();
    }

    private Control BuildHero()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(18)
        };

        var title = new Label
        {
            Text = "Southern Pride Towing",
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true
        };
        title.Font = new Font(title.Font.FontFamily, 18, FontStyle.Bold);

        var subtitle = new Label
        {
            Text = "Fast estimates, clean invoices, and a searchable quote history.",
            ForeColor = Color.FromArgb(203, 213, 245),
            AutoSize = true
        };

        _heroYard.Text = _prefs.YardAddress;
        _heroYard.ForeColor = Color.FromArgb(226, 232, 240);
        _heroYard.Font = new Font(Font.FontFamily, 9, FontStyle.Bold);
        _heroYard.AutoSize = true;

        var textPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        textPanel.Controls.Add(title);
        textPanel.Controls.Add(subtitle);
        textPanel.Controls.Add(_heroYard);
        panel.Controls.Add(textPanel);

        return panel;
    }

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var estimatorTab = new TabPage("Estimator") { BackColor = Color.FromArgb(241, 245, 249), AutoScroll = true };
        var logTab = new TabPage("Quote Log") { BackColor = Color.FromArgb(241, 245, 249), AutoScroll = true };

        estimatorTab.Controls.Add(BuildEstimatorPage());
        logTab.Controls.Add(BuildLogSection());

        tabs.TabPages.Add(estimatorTab);
        tabs.TabPages.Add(logTab);
        return tabs;
    }

    private Control BuildEstimatorPage()
    {
        var section = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
        };
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 12, 8, 0) };
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 12, 0, 0) };

        leftPanel.Controls.Add(BuildRoutePanel());
        rightPanel.Controls.Add(BuildSummaryPanel());

        section.Controls.Add(leftPanel, 0, 0);
        section.Controls.Add(rightPanel, 1, 0);

        return section;
    }

    private Control BuildRoutePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
        panel.BorderStyle = BorderStyle.FixedSingle;

        var heading = new Label
        {
            Text = "Route & Pricing",
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            AutoSize = true
        };
        panel.Controls.Add(heading);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Top = 32,
            ColumnCount = 2,
            RowCount = 15,
            Padding = new Padding(0, 28, 0, 0)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        panel.Controls.Add(layout);

        AddSectionLabel(layout, "Route details", 0);

        _yardInfoLabel.Text = $"Default yard: {_prefs.YardAddress}";
        _yardInfoLabel.ForeColor = Color.FromArgb(100, 116, 139);
        _yardInfoLabel.AutoSize = true;
        layout.Controls.Add(_yardInfoLabel, 0, 1);
        layout.SetColumnSpan(_yardInfoLabel, 2);

        _deadheadInput.PlaceholderText = "Your yard address";
        layout.Controls.Add(CreateAutocompleteField("Deadhead start (your yard/base)", _deadheadInput), 0, 2);
        layout.SetColumnSpan(layout.Controls[^1], 2);

        var defaultTogglePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };
        _useDefaultYardToggle.Text = "Use default yard for deadhead";
        _useDefaultYardToggle.AutoSize = true;
        _useDefaultYardToggle.CheckedChanged += (_, _) =>
        {
            ApplyDefaultYardToggle();
            SavePreferences();
        };
        defaultTogglePanel.Controls.Add(_useDefaultYardToggle);
        layout.Controls.Add(defaultTogglePanel, 0, 3);
        layout.SetColumnSpan(defaultTogglePanel, 2);

        _pickupInput.PlaceholderText = "Pickup address";
        layout.Controls.Add(CreateAutocompleteField("Pickup address", _pickupInput), 0, 4);
        layout.SetColumnSpan(layout.Controls[^1], 2);

        _dropoffInput.PlaceholderText = "Drop-off address";
        layout.Controls.Add(CreateAutocompleteField("Drop-off address", _dropoffInput), 0, 5);
        layout.SetColumnSpan(layout.Controls[^1], 2);

        AddSectionLabel(layout, "Customer details", 6);

        _customerNameInput.PlaceholderText = "Customer name";
        layout.Controls.Add(CreateTextField("Customer name", _customerNameInput), 0, 7);
        layout.SetColumnSpan(layout.Controls[^1], 2);

        var phonePanel = new Panel { Dock = DockStyle.Top, Height = 60 };
        var phoneLabel = new Label { Text = "Phone numbers", AutoSize = true };
        phoneLabel.Dock = DockStyle.Top;
        var phoneRow = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2
        };
        phoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        phoneRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _customerPhonePrimaryInput.PlaceholderText = "Primary phone";
        _customerPhoneSecondaryInput.PlaceholderText = "Secondary phone";
        _customerPhonePrimaryInput.Dock = DockStyle.Fill;
        _customerPhoneSecondaryInput.Dock = DockStyle.Fill;
        phoneRow.Controls.Add(_customerPhonePrimaryInput, 0, 0);
        phoneRow.Controls.Add(_customerPhoneSecondaryInput, 1, 0);
        phonePanel.Controls.Add(phoneRow);
        phonePanel.Controls.Add(phoneLabel);
        layout.Controls.Add(phonePanel, 0, 8);
        layout.SetColumnSpan(phonePanel, 2);

        AddSectionLabel(layout, "Pricing controls", 9);

        layout.Controls.Add(CreateNumericField("Hook/Base Fee ($)", _baseFeeInput, 0, 1000, 1, 0), 0, 10);
        layout.Controls.Add(CreateNumericField("Rate per Mile ($)", _rateInput, 0, 1000, 0.01m, 2), 1, 10);

        layout.Controls.Add(CreateNumericField("Discount %", _discountPercentInput, 0, 100, 0.1m, 1), 0, 11);
        layout.Controls.Add(CreateNumericField("Discount $", _discountAmountInput, 0, 1000, 0.01m, 2), 1, 11);

        var discountReasonPanel = CreateTextField("Discount reason", _discountReasonInput);
        layout.Controls.Add(discountReasonPanel, 0, 12);
        layout.SetColumnSpan(discountReasonPanel, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true
        };
        var calcButton = new Button
        {
            Text = "Get Estimate",
            AutoSize = false,
            Width = 240,
            Height = 36,
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.White
        };
        calcButton.Click += async (_, _) => await RunEstimateAsync();

        var printButton = new Button
        {
            Text = "Print Estimate",
            AutoSize = false,
            Width = 240,
            Height = 36,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        printButton.Click += (_, _) => PrintEstimate();

        buttonPanel.Controls.Add(calcButton);
        buttonPanel.Controls.Add(printButton);

        var clearButton = new Button
        {
            Text = "Clear Form",
            AutoSize = false,
            Width = 240,
            Height = 36,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        clearButton.Click += (_, _) => ClearForm();
        buttonPanel.Controls.Add(clearButton);

        layout.Controls.Add(buttonPanel, 0, 13);
        layout.SetColumnSpan(buttonPanel, 2);

        AttachAutocomplete(_deadheadInput, point => _deadheadPoint = point);
        AttachAutocomplete(_pickupInput, point => _pickupPoint = point);
        AttachAutocomplete(_dropoffInput, point => _dropoffPoint = point);

        _toolTip.SetToolTip(_discountPercentInput, "Percent discount applied to base + mileage.");
        _toolTip.SetToolTip(_discountAmountInput, "Flat discount amount applied after percent.");
        _toolTip.SetToolTip(_discountReasonInput, "Optional notes for the discount.");

        return panel;
    }

    private Control BuildSummaryPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
        panel.BorderStyle = BorderStyle.FixedSingle;

        var badge = new Label
        {
            Text = "Estimate Summary",
            AutoSize = true,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(6)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(badge);

        _resultBox.Multiline = true;
        _resultBox.ReadOnly = true;
        _resultBox.BackColor = Color.FromArgb(239, 246, 255);
        _resultBox.BorderStyle = BorderStyle.FixedSingle;
        _resultBox.ScrollBars = ScrollBars.Vertical;
        _resultBox.Dock = DockStyle.Fill;
        layout.Controls.Add(_resultBox);
        panel.Controls.Add(layout);

        return panel;
    }

    private Control BuildLogSection()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16) };
        panel.BorderStyle = BorderStyle.FixedSingle;

        var title = new Label
        {
            Text = "Quote Log",
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            AutoSize = true
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);
        layout.Controls.Add(title);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 32, 0, 0)
        };

        var dateLabel = new Label { Text = "Filter by date", AutoSize = true, Margin = new Padding(0, 6, 6, 0) };
        _logDate.Format = DateTimePickerFormat.Short;
        _logDate.ShowCheckBox = true;
        _logDate.Checked = false;
        _logDate.ValueChanged += (_, _) => RenderLogs();

        var searchLabel = new Label { Text = "Search", AutoSize = true, Margin = new Padding(12, 6, 6, 0) };
        _logSearch.Width = 220;
        _logSearch.TextChanged += (_, _) => RenderLogs();

        var clearButton = new Button { Text = "Clear Filters" };
        clearButton.Click += (_, _) =>
        {
            _logDate.Checked = false;
            _logSearch.Text = string.Empty;
            RenderLogs();
        };

        filters.Controls.Add(dateLabel);
        filters.Controls.Add(_logDate);
        filters.Controls.Add(searchLabel);
        filters.Controls.Add(_logSearch);
        filters.Controls.Add(clearButton);
        layout.Controls.Add(filters);

        _logList.Dock = DockStyle.Fill;
        _logList.View = View.Details;
        _logList.FullRowSelect = true;
        _logList.Columns.Add("When", 160);
        _logList.Columns.Add("Customer", 180);
        _logList.Columns.Add("Phone(s)", 180);
        _logList.Columns.Add("Pickup", 220);
        _logList.Columns.Add("Dropoff", 220);
        _logList.Columns.Add("Total", 100, HorizontalAlignment.Right);
        _logList.Columns.Add("Miles", 80, HorizontalAlignment.Right);
        _logList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        layout.Controls.Add(_logList);

        _logEmpty.Text = "No quotes yet. Run an estimate to save it here.";
        _logEmpty.AutoSize = true;
        _logEmpty.ForeColor = Color.FromArgb(100, 116, 139);
        _logEmpty.Dock = DockStyle.Bottom;
        panel.Controls.Add(_logEmpty);

        return panel;
    }

    private void AddSectionLabel(TableLayoutPanel layout, string text, int row)
    {
        var label = new Label
        {
            Text = text.ToUpperInvariant(),
            Font = new Font(Font.FontFamily, 8, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 6)
        };
        layout.Controls.Add(label, 0, row);
        layout.SetColumnSpan(label, 2);
    }

    private static Panel CreateTextField(string labelText, TextBox input)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 60 };
        var label = new Label { Text = labelText, AutoSize = true };
        label.Dock = DockStyle.Top;
        input.Dock = DockStyle.Bottom;
        panel.Controls.Add(input);
        panel.Controls.Add(label);
        return panel;
    }

    private static Panel CreateNumericField(string labelText, NumericUpDown input, decimal min, decimal max, decimal increment, int decimals)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 60 };
        var label = new Label { Text = labelText, AutoSize = true };
        label.Dock = DockStyle.Top;
        input.DecimalPlaces = decimals;
        input.Minimum = min;
        input.Maximum = max;
        input.Increment = increment;
        input.Dock = DockStyle.Bottom;
        panel.Controls.Add(input);
        panel.Controls.Add(label);
        return panel;
    }

    private Panel CreateAutocompleteField(string labelText, TextBox input)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 90 };
        var label = new Label { Text = labelText, AutoSize = true };
        label.Dock = DockStyle.Top;
        input.Dock = DockStyle.Top;

        var listBox = new ListBox
        {
            Visible = false,
            Dock = DockStyle.Bottom,
            Height = 90
        };
        listBox.Click += (_, _) =>
        {
            if (listBox.SelectedItem is GeoPoint item)
            {
                input.Text = item.Formatted;
                if (listBox.Tag is Action<GeoPoint?> onSelect)
                {
                    onSelect(item);
                }
            }
            listBox.Visible = false;
        };

        _autocompleteLists[input] = listBox;

        panel.Controls.Add(listBox);
        panel.Controls.Add(input);
        panel.Controls.Add(label);
        return panel;
    }

    private void AttachAutocomplete(TextBox input, Action<GeoPoint?> setPoint)
    {
        if (_autocompleteLists.TryGetValue(input, out var listBox))
        {
            listBox.Tag = setPoint;
            listBox.SelectedIndexChanged += (_, _) =>
            {
                if (listBox.SelectedItem is GeoPoint selected)
                {
                    setPoint(selected);
                }
            };
        }

        input.TextChanged += (_, _) =>
        {
            setPoint(null);
            StartAutocomplete(input);
        };
        input.Leave += (_, _) =>
        {
            if (_autocompleteLists.TryGetValue(input, out var listBox))
            {
                var timer = new System.Windows.Forms.Timer { Interval = 150 };
                timer.Tick += (_, _) =>
                {
                    listBox.Visible = false;
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
        };
    }

    private void StartAutocomplete(TextBox input)
    {
        if (!_autocompleteLists.TryGetValue(input, out var listBox))
        {
            return;
        }

        if (_autocompleteTokens.TryGetValue(input, out var existing))
        {
            existing.Cancel();
            existing.Dispose();
        }

        var cts = new CancellationTokenSource();
        _autocompleteTokens[input] = cts;
        var token = cts.Token;

        var timer = new System.Windows.Forms.Timer { Interval = 350 };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                if (input.Text.Trim().Length < 3)
                {
                    listBox.Visible = false;
                    return;
                }
                var results = await _geoClient.AutocompleteAsync(input.Text, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                listBox.DataSource = results.ToList();
                listBox.DisplayMember = nameof(GeoPoint.Formatted);
                listBox.Visible = results.Count > 0;
                listBox.ClearSelected();
            }
            catch
            {
                listBox.Visible = false;
            }
        };
        timer.Start();
    }

    private void LoadPreferencesIntoUi()
    {
        _useDefaultYardToggle.Checked = _prefs.UseDefaultYard;
        _deadheadInput.Text = _prefs.UseDefaultYard
            ? _prefs.YardAddress
            : string.IsNullOrWhiteSpace(_prefs.CustomDeadheadAddress)
                ? _prefs.YardAddress
                : _prefs.CustomDeadheadAddress;
        _baseFeeInput.Value = _prefs.BaseFee;
        _rateInput.Value = _prefs.RatePerMile;
        _discountPercentInput.Value = _prefs.DiscountPercent;
        _discountAmountInput.Value = _prefs.DiscountAmount;
        _discountReasonInput.Text = _prefs.DiscountReason;
        _customerNameInput.Text = string.Empty;
        _customerPhonePrimaryInput.Text = string.Empty;
        _customerPhoneSecondaryInput.Text = string.Empty;
        _heroYard.Text = _prefs.YardAddress;
        _yardInfoLabel.Text = $"Default yard: {_prefs.YardAddress}";
        ApplyDefaultYardToggle();

        _deadheadInput.Leave += (_, _) => SavePreferences();
        _baseFeeInput.ValueChanged += (_, _) => SavePreferences();
        _rateInput.ValueChanged += (_, _) => SavePreferences();
        _discountPercentInput.ValueChanged += (_, _) => SavePreferences();
        _discountAmountInput.ValueChanged += (_, _) => SavePreferences();
        _discountReasonInput.Leave += (_, _) => SavePreferences();
    }

    private void SavePreferences()
    {
        _prefs.UseDefaultYard = _useDefaultYardToggle.Checked;
        if (_deadheadInput.Enabled)
        {
            _prefs.CustomDeadheadAddress = _deadheadInput.Text.Trim();
        }
        _prefs.BaseFee = _baseFeeInput.Value;
        _prefs.RatePerMile = _rateInput.Value;
        _prefs.DiscountPercent = _discountPercentInput.Value;
        _prefs.DiscountAmount = _discountAmountInput.Value;
        _prefs.DiscountReason = _discountReasonInput.Text.Trim();
        _storage.SavePreferences(_prefs);
        _heroYard.Text = _prefs.YardAddress;
        _yardInfoLabel.Text = $"Default yard: {_prefs.YardAddress}";
    }

    private void ClearForm()
    {
        _pickupInput.Text = string.Empty;
        _dropoffInput.Text = string.Empty;
        _customerNameInput.Text = string.Empty;
        _customerPhonePrimaryInput.Text = string.Empty;
        _customerPhoneSecondaryInput.Text = string.Empty;
        _discountPercentInput.Value = 0;
        _discountAmountInput.Value = 0;
        _discountReasonInput.Text = string.Empty;
        _resultBox.Text = string.Empty;
        _pickupPoint = null;
        _dropoffPoint = null;
        if (!_useDefaultYardToggle.Checked)
        {
            _deadheadInput.Text = string.Empty;
            _deadheadPoint = null;
        }
        ApplyDefaultYardToggle();
    }

    private void ApplyDefaultYardToggle()
    {
        var useDefault = _useDefaultYardToggle.Checked;
        _deadheadInput.Enabled = !useDefault;
        if (useDefault)
        {
            _deadheadInput.Text = _prefs.YardAddress;
        }
        else if (string.IsNullOrWhiteSpace(_deadheadInput.Text))
        {
            _deadheadInput.Text = string.IsNullOrWhiteSpace(_prefs.CustomDeadheadAddress)
                ? _prefs.YardAddress
                : _prefs.CustomDeadheadAddress;
        }
    }

    private async Task RunEstimateAsync()
    {
        _resultBox.Text = "Calculating...";

        var baseFee = _baseFeeInput.Value;
        var rate = _rateInput.Value;
        var discountPercent = Math.Max(0, _discountPercentInput.Value);
        var discountAmount = Math.Max(0, _discountAmountInput.Value);
        var discountReason = _discountReasonInput.Text.Trim();
        var customerName = _customerNameInput.Text.Trim();
        var phonePrimary = _customerPhonePrimaryInput.Text.Trim();
        var phoneSecondary = _customerPhoneSecondaryInput.Text.Trim();

        try
        {
            var dead = await EnsurePointAsync(_deadheadInput.Text, _deadheadPoint);
            var pickup = await EnsurePointAsync(_pickupInput.Text, _pickupPoint);
            var dropoff = await EnsurePointAsync(_dropoffInput.Text, _dropoffPoint);

            if (dead is null || pickup is null || dropoff is null)
            {
                _resultBox.Text = "Please provide valid addresses.";
                return;
            }

            _deadheadPoint = dead;
            _pickupPoint = pickup;
            _dropoffPoint = dropoff;

            var deadMi = await _geoClient.RouteMilesAsync(dead, pickup, CancellationToken.None);
            var towMi = await _geoClient.RouteMilesAsync(pickup, dropoff, CancellationToken.None);
            var totalMi = deadMi + towMi;
            var mileageCost = rate * (decimal)totalMi;
            var preDiscount = baseFee + mileageCost;
            var percentDiscount = preDiscount * (discountPercent / 100m);
            var discountTotal = Math.Min(preDiscount, percentDiscount + discountAmount);
            var totalCost = preDiscount - discountTotal;

            var summary = new StringBuilder();
            summary.AppendLine($"Deadhead: {deadMi:F1} mi");
            summary.AppendLine($"Tow: {towMi:F1} mi");
            summary.AppendLine($"Total miles: {totalMi:F1} mi");
            summary.AppendLine();
            summary.AppendLine($"Base fee: {FormatMoney(baseFee)}");
            summary.AppendLine($"Mileage: {FormatMoney(mileageCost)}");
            summary.AppendLine($"Discounts: -{FormatMoney(discountTotal)}");
            summary.AppendLine();
            summary.AppendLine($"Estimated price: {FormatMoney(totalCost)}");
            summary.AppendLine();
            summary.AppendLine($"From: {pickup.Formatted}");
            summary.AppendLine($"To:   {dropoff.Formatted}");

            _resultBox.Text = summary.ToString();

            var entry = new LogEntry
            {
                Id = Guid.NewGuid(),
                When = DateTimeOffset.Now,
                CustomerName = customerName,
                PhonePrimary = phonePrimary,
                PhoneSecondary = phoneSecondary,
                Pickup = pickup.Formatted,
                Dropoff = dropoff.Formatted,
                Deadhead = dead.Formatted,
                DeadheadMiles = deadMi,
                TowMiles = towMi,
                TotalMiles = totalMi,
                BaseFee = baseFee,
                RatePerMile = rate,
                MileageCost = mileageCost,
                DiscountPercent = discountPercent,
                DiscountAmount = discountAmount,
                DiscountTotal = discountTotal,
                TotalCost = totalCost,
                Notes = discountReason
            };

            _lastEstimate = entry;
            AddLog(entry);
        }
        catch (Exception ex)
        {
            _resultBox.Text = $"Could not get routing distance. {ex.Message}";
        }
    }

    private async Task<GeoPoint?> EnsurePointAsync(string text, GeoPoint? cached)
    {
        if (cached is not null && string.Equals(cached.Formatted, text, StringComparison.OrdinalIgnoreCase))
        {
            return cached;
        }

        return await _geoClient.GeocodeFirstAsync(text, CancellationToken.None);
    }

    private void AddLog(LogEntry entry)
    {
        _logs.Insert(0, entry);
        _storage.SaveLogs(_logs);
        RenderLogs();
    }

    private void RenderLogs()
    {
        _logList.Items.Clear();
        var search = _logSearch.Text.Trim().ToLowerInvariant();
        var filtered = _logs.Where(entry =>
        {
            var matchesDate = !_logDate.Checked || entry.When.Date == _logDate.Value.Date;
            var haystack = $"{entry.CustomerName} {entry.PhonePrimary} {entry.PhoneSecondary} {entry.Pickup} {entry.Dropoff} {entry.Deadhead} {entry.Notes} {entry.TotalCost}".ToLowerInvariant();
            var matchesSearch = string.IsNullOrWhiteSpace(search) || haystack.Contains(search);
            return matchesDate && matchesSearch;
        }).ToList();

        foreach (var entry in filtered)
        {
            var item = new ListViewItem(entry.When.LocalDateTime.ToString(CultureInfo.CurrentCulture));
            var phones = string.IsNullOrWhiteSpace(entry.PhoneSecondary)
                ? entry.PhonePrimary
                : $"{entry.PhonePrimary} / {entry.PhoneSecondary}";
            item.SubItems.Add(string.IsNullOrWhiteSpace(entry.CustomerName) ? "-" : entry.CustomerName);
            item.SubItems.Add(string.IsNullOrWhiteSpace(phones) ? "-" : phones);
            item.SubItems.Add(entry.Pickup);
            item.SubItems.Add(entry.Dropoff);
            item.SubItems.Add(FormatMoney(entry.TotalCost));
            item.SubItems.Add(entry.TotalMiles.ToString("F1", CultureInfo.CurrentCulture));
            _logList.Items.Add(item);
        }

        _logEmpty.Visible = filtered.Count == 0;
    }

    private void PrintEstimate()
    {
        if (_lastEstimate is null)
        {
            MessageBox.Show("Run an estimate first.", "Print Estimate", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var document = new PrintDocument();
        document.PrintPage += (_, e) => RenderPrintPage(e, _lastEstimate);

        using var dialog = new PrintDialog
        {
            Document = document
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            document.Print();
        }
    }

    private void RenderPrintPage(PrintPageEventArgs args, LogEntry entry)
    {
        var g = args.Graphics;
        if (g is null)
        {
            return;
        }
        var font = new Font("Segoe UI", 10);
        var bold = new Font("Segoe UI", 12, FontStyle.Bold);
        float y = 40;

        g.DrawString("Southern Pride Towing", bold, Brushes.Black, 40, y);
        y += 26;
        g.DrawString("Estimate", font, Brushes.Black, 40, y);
        y += 24;

        g.DrawString($"Date: {entry.When.LocalDateTime}", font, Brushes.Black, 40, y);
        y += 18;
        g.DrawString($"Pickup: {entry.Pickup}", font, Brushes.Black, 40, y);
        y += 18;
        g.DrawString($"Drop-off: {entry.Dropoff}", font, Brushes.Black, 40, y);
        y += 18;
        g.DrawString($"Deadhead: {entry.Deadhead}", font, Brushes.Black, 40, y);
        y += 24;

        g.DrawString($"Deadhead {entry.DeadheadMiles:F1} mi, Tow {entry.TowMiles:F1} mi, Total {entry.TotalMiles:F1} mi", font, Brushes.Black, 40, y);
        y += 24;

        g.DrawString($"Base fee: {FormatMoney(entry.BaseFee)}", font, Brushes.Black, 40, y);
        y += 18;
        g.DrawString($"Rate per mile: {FormatMoney(entry.RatePerMile)}/mi", font, Brushes.Black, 40, y);
        y += 18;
        g.DrawString($"Discounts: -{FormatMoney(entry.DiscountTotal)}", font, Brushes.Black, 40, y);
        y += 18;
        g.DrawString($"Estimated total: {FormatMoney(entry.TotalCost)}", bold, Brushes.Black, 40, y);
    }

    private static string FormatMoney(decimal value)
    {
        return value.ToString("C2", CultureInfo.CurrentCulture);
    }
}
