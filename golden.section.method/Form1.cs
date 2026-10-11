using DichotomyApp;
using ScottPlot.WinForms;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace GoldenSectionMethod
{
  public class Form1 : Form
  {
    private TextBox _textBoxA;
    private TextBox _textBoxB;
    private TextBox _textBoxE;
    private TextBox _textBoxFormula;

    private RadioButton _radioMin;
    private RadioButton _radioMax;
    private RadioButton _radioRoot;

    private FormsPlot _formsPlot;

    private Label _labelResult;

    private Label _labelRootName;
    private Label _valueRoot;
    private Label _labelFunctionName;
    private Label _valueFunction;
    private Label _valueIterations;
    private Label _valuePrecision;

    private TableLayoutPanel _resultTable;
    private TableLayoutPanel _leftPanel;

    private MenuStrip _menuStrip;
    private Label _helpIcon;
    private ToolTip _formulaToolTip;

    private System.Windows.Forms.Timer _autoCalcTimer;

    private double _leftBound;
    private double _rightBound;
    private double _precision;

    private FunctionParser _parser;
    private GoldenSectionResult _solverResult;

    public Form1()
    {
      BuildUserInterface();

      _autoCalcTimer = new System.Windows.Forms.Timer { Interval = 250 };

      _autoCalcTimer.Tick += (sender, args) =>
      {
        _autoCalcTimer.Stop();
        TryAutoCalculate();
      };

      _textBoxA.TextChanged += (sender, args) => ScheduleAutoCalculate();
      _textBoxB.TextChanged += (sender, args) => ScheduleAutoCalculate();
      _textBoxE.TextChanged += (sender, args) => ScheduleAutoCalculate();
      _textBoxFormula.TextChanged += (sender, args) => ScheduleAutoCalculate();

      _radioMin.CheckedChanged += (sender, args) => ScheduleAutoCalculate();
      _radioMax.CheckedChanged += (sender, args) => ScheduleAutoCalculate();
      _radioRoot.CheckedChanged += (sender, args) => ScheduleAutoCalculate();
    }

    private OptimizationMode CurrentMode
    {
      get
      {
        if (_radioMax.Checked) return OptimizationMode.Maximum;
        if (_radioRoot.Checked) return OptimizationMode.Root;
        return OptimizationMode.Minimum;
      }
    }

    // ============================================================
    // ИНТЕРФЕЙС
    // ============================================================

    private void BuildUserInterface()
    {
      Text = "Метод золотого сечения";
      Width = 1200;
      Height = 720;
      MinimumSize = new Size(950, 620);
      StartPosition = FormStartPosition.CenterScreen;
      Font = new Font("Segoe UI", 9F, FontStyle.Regular);

      // MENU STRIP
      _menuStrip = new MenuStrip();

      var calculateItem = new ToolStripMenuItem("Рассчитать");
      var plotItem = new ToolStripMenuItem("Построить график");
      var clearItem = new ToolStripMenuItem("Очистить");

      calculateItem.Click += (sender, args) => Calculate();
      plotItem.Click += (sender, args) => BuildGraphOnly();
      clearItem.Click += (sender, args) => ClearAll();

      _menuStrip.Items.Add(calculateItem);
      _menuStrip.Items.Add(plotItem);
      _menuStrip.Items.Add(clearItem);

      MainMenuStrip = _menuStrip;
      Controls.Add(_menuStrip);

      // ОСНОВНОЙ LAYOUT
      var rootLayout = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 1,
        Padding = new Padding(14)
      };

      rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));
      rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

      Controls.Add(rootLayout);

      // ЛЕВАЯ ПАНЕЛЬ
      _leftPanel = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 3,
        RowCount = 9,
        Padding = new Padding(0)
      };

      _leftPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
      _leftPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
      _leftPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));

      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 0 a
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 1 b
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 2 e
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 3 f(x)
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // 4 режим
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 10)); // 5 отступ
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); // 6 заголовок
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));// 7 таблица
      _leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 8 filler

      // a
      _leftPanel.Controls.Add(MakeLabel("a ="), 0, 0);
      _textBoxA = MakeTextBox();
      _leftPanel.Controls.Add(_textBoxA, 1, 0);
      _leftPanel.SetColumnSpan(_textBoxA, 2);

      // b
      _leftPanel.Controls.Add(MakeLabel("b ="), 0, 1);
      _textBoxB = MakeTextBox();
      _leftPanel.Controls.Add(_textBoxB, 1, 1);
      _leftPanel.SetColumnSpan(_textBoxB, 2);

      // e
      _leftPanel.Controls.Add(MakeLabel("e ="), 0, 2);
      _textBoxE = MakeTextBox();
      _textBoxE.Text = "0,0001";
      _leftPanel.Controls.Add(_textBoxE, 1, 2);
      _leftPanel.SetColumnSpan(_textBoxE, 2);

      // f(x)
      _leftPanel.Controls.Add(MakeLabel("f(x) ="), 0, 3);
      _textBoxFormula = MakeTextBox();
      _textBoxFormula.Text = "x^2 + 2*x - 6";
      _leftPanel.Controls.Add(_textBoxFormula, 1, 3);

      // ?
      _helpIcon = new Label
      {
        Text = "?",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        ForeColor = Color.White,
        BackColor = Color.SteelBlue,
        Cursor = Cursors.Help,
        Margin = new Padding(4, 6, 0, 6)
      };

      _leftPanel.Controls.Add(_helpIcon, 2, 3);

      // РЕЖИМ
      _leftPanel.Controls.Add(MakeLabel("Режим:"), 0, 4);

      var modePanel = new FlowLayoutPanel
      {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        Margin = new Padding(0, 4, 0, 4),
        Padding = new Padding(0)
      };

      _radioMin = new RadioButton
      {
        Text = "Минимум",
        Checked = true,
        AutoSize = true,
        Margin = new Padding(0, 6, 10, 0)
      };
      _radioMax = new RadioButton
      {
        Text = "Максимум",
        AutoSize = true,
        Margin = new Padding(0, 6, 10, 0)
      };
      _radioRoot = new RadioButton
      {
        Text = "Корень",
        AutoSize = true,
        Margin = new Padding(0, 6, 0, 0)
      };

      modePanel.Controls.Add(_radioMin);
      modePanel.Controls.Add(_radioMax);
      modePanel.Controls.Add(_radioRoot);

      _leftPanel.Controls.Add(modePanel, 1, 4);
      _leftPanel.SetColumnSpan(modePanel, 2);

      // РЕЗУЛЬТАТ
      _labelResult = new Label
      {
        Text = "Результат",
        Dock = DockStyle.Fill,
        AutoSize = false,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        ForeColor = Color.DarkGreen,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0, 4, 0, 4),
        Margin = new Padding(0)
      };

      _leftPanel.Controls.Add(_labelResult, 0, 6);
      _leftPanel.SetColumnSpan(_labelResult, 3);

      _resultTable = CreateResultTable();
      _leftPanel.Controls.Add(_resultTable, 0, 7);
      _leftPanel.SetColumnSpan(_resultTable, 3);

      rootLayout.Controls.Add(_leftPanel, 0, 0);

      // ГРАФИК
      _formsPlot = new FormsPlot { Dock = DockStyle.Fill };
      rootLayout.Controls.Add(_formsPlot, 1, 0);

      // TOOLTIP
      _formulaToolTip = new ToolTip
      {
        AutoPopDelay = 60000,
        InitialDelay = 150,
        ReshowDelay = 50,
        ShowAlways = true,
        IsBalloon = false,
        ToolTipTitle = "Поддерживаемые формулы"
      };

      _formulaToolTip.SetToolTip(_helpIcon, BuildFormulaTooltipText());
    }

    // ============================================================
    // ТАБЛИЦА РЕЗУЛЬТАТА
    // ============================================================

    private TableLayoutPanel CreateResultTable()
    {
      var table = new TableLayoutPanel
      {
        Dock = DockStyle.Fill,
        ColumnCount = 2,
        RowCount = 4,
        Margin = new Padding(0),
        Padding = new Padding(0)
      };

      table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
      table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

      for (int i = 0; i < 4; i++)
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));

      _labelRootName = CreateResultNameLabel("Точка минимума");
      table.Controls.Add(_labelRootName, 0, 0);
      _valueRoot = CreateResultValueLabel();
      table.Controls.Add(_valueRoot, 1, 0);

      _labelFunctionName = CreateResultNameLabel("f(x_min)");
      table.Controls.Add(_labelFunctionName, 0, 1);
      _valueFunction = CreateResultValueLabel();
      table.Controls.Add(_valueFunction, 1, 1);

      table.Controls.Add(CreateResultNameLabel("Итераций"), 0, 2);
      _valueIterations = CreateResultValueLabel();
      table.Controls.Add(_valueIterations, 1, 2);

      table.Controls.Add(CreateResultNameLabel("Точность e"), 0, 3);
      _valuePrecision = CreateResultValueLabel();
      table.Controls.Add(_valuePrecision, 1, 3);

      return table;
    }

    private Label CreateResultNameLabel(string text)
    {
      return new Label
      {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0)
      };
    }

    private Label CreateResultValueLabel()
    {
      return new Label
      {
        Text = "-",
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9F, FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(0)
      };
    }

    // ============================================================
    // РЕЗУЛЬТАТ
    // ============================================================

    private void UpdateResultLabels(OptimizationMode mode)
    {
      switch (mode)
      {
        case OptimizationMode.Minimum:
          _labelRootName.Text = "Точка минимума";
          _labelFunctionName.Text = "f(x_min)";
          break;
        case OptimizationMode.Maximum:
          _labelRootName.Text = "Точка максимума";
          _labelFunctionName.Text = "f(x_max)";
          break;
        case OptimizationMode.Root:
          _labelRootName.Text = "Корень";
          _labelFunctionName.Text = "f(x)";
          break;
      }
    }

    private void SetResultValues(string root, string functionValue,
                                 string iterations, string precision)
    {
      _valueRoot.Text = root;
      _valueFunction.Text = functionValue;
      _valueIterations.Text = iterations;
      _valuePrecision.Text = precision;
    }

    private void ClearResultTable()
    {
      SetResultValues("-", "-", "-", "-");
    }

    private void ShowResultTable()
    {
      _resultTable.Visible = true;
      _leftPanel.RowStyles[7].Height = 116;
    }

    private void HideResultTable()
    {
      _resultTable.Visible = false;
      _leftPanel.RowStyles[7].Height = 0;
    }

    // ============================================================
    // LABEL / TEXTBOX
    // ============================================================

    private Label MakeLabel(string caption)
    {
      return new Label
      {
        Text = caption,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Padding = new Padding(0, 0, 8, 0),
        Margin = new Padding(0)
      };
    }

    private TextBox MakeTextBox()
    {
      return new TextBox
      {
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 10F, FontStyle.Regular),
        Margin = new Padding(0, 6, 0, 6)
      };
    }

    // ============================================================
    // TOOLTIP
    // ============================================================

    private static string BuildFormulaTooltipText()
    {
      return
          "ОПЕРАТОРЫ:\r\n" +
          "  + - * / ^  и  ( )\r\n" +
          "\r\n" +
          "ФУНКЦИИ:\r\n" +
          "  sin(x)   cos(x)   tan(x)\r\n" +
          "  exp(x)   ln(x)    log(x)\r\n" +
          "  sqrt(x)  abs(x)\r\n" +
          "\r\n" +
          "КОНСТАНТЫ:\r\n" +
          "  pi ≈ 3.14159    e ≈ 2.71828\r\n" +
          "\r\n" +
          "ПРИМЕРЫ:\r\n" +
          "  x^2 + 2*x - 6\r\n" +
          "  sin(x) - 0.5\r\n" +
          "  sqrt(x) - 1\r\n" +
          "  ln(x) - 1\r\n" +
          "\r\n" +
          "Десятичный разделитель в формуле: точка.\r\n" +
          "Для a, b и e можно использовать точку или запятую.\r\n" +
          "Тригонометрия — в радианах.";
    }

    // ============================================================
    // АВТОРАСЧЁТ
    // ============================================================

    private void ScheduleAutoCalculate()
    {
      _autoCalcTimer.Stop();
      _autoCalcTimer.Start();
    }

    // ============================================================
    // ОШИБКА
    // ============================================================

    private void ShowError(string message)
    {
      _labelResult.ForeColor = Color.DarkRed;
      _labelResult.Text = "Ошибка: " + message;

      ClearResultTable();
      HideResultTable();
    }

    // ============================================================
    // ОЧИСТКА
    // ============================================================

    private void ClearAll()
    {
      _textBoxA.Text = "";
      _textBoxB.Text = "";
      _textBoxE.Text = "0,0001";
      _textBoxFormula.Text = "";

      ResetFieldColors();

      _labelResult.ForeColor = Color.DarkGreen;
      _labelResult.Text = "Результат";

      ClearResultTable();
      ShowResultTable();

      _solverResult = null;
      _parser = null;

      _formsPlot.Plot.Clear();
      _formsPlot.Plot.Title("");
      _formsPlot.Refresh();
    }

    // ============================================================
    // ЦВЕТА ПОЛЕЙ
    // ============================================================

    private void ResetFieldColors()
    {
      _textBoxA.BackColor = SystemColors.Window;
      _textBoxB.BackColor = SystemColors.Window;
      _textBoxE.BackColor = SystemColors.Window;
      _textBoxFormula.BackColor = SystemColors.Window;
    }

    private void MarkInvalid(TextBox textBox, bool isInvalid)
    {
      textBox.BackColor = isInvalid ? Color.MistyRose : SystemColors.Window;
    }

    // ============================================================
    // РАСЧЁТ
    // ============================================================

    private void Calculate()
    {
      ResetFieldColors();

      if (!TryParseDouble(_textBoxA.Text, out _leftBound))
      { MarkInvalid(_textBoxA, true); ShowError("некорректное значение a."); return; }

      if (!TryParseDouble(_textBoxB.Text, out _rightBound))
      { MarkInvalid(_textBoxB, true); ShowError("некорректное значение b."); return; }

      if (!TryParseDouble(_textBoxE.Text, out _precision))
      { MarkInvalid(_textBoxE, true); ShowError("некорректное значение e."); return; }

      if (_leftBound >= _rightBound)
      {
        MarkInvalid(_textBoxA, true);
        MarkInvalid(_textBoxB, true);
        ShowError("должно выполняться a < b.");
        return;
      }

      if (_precision <= 0)
      { MarkInvalid(_textBoxE, true); ShowError("точность e должна быть больше 0."); return; }

      if (string.IsNullOrWhiteSpace(_textBoxFormula.Text))
      { MarkInvalid(_textBoxFormula, true); ShowError("введите формулу."); return; }

      try
      {
        _parser = new FunctionParser(_textBoxFormula.Text);
        _parser.Evaluate(_leftBound);
        _parser.Evaluate(_rightBound);
      }
      catch (Exception exception)
      {
        MarkInvalid(_textBoxFormula, true);
        ShowError("ошибка формулы: " + exception.Message);
        return;
      }

      _solverResult = GoldenSectionSolver.Solve(
          _parser.Evaluate, _leftBound, _rightBound, _precision, CurrentMode);

      ShowResult();
      UpdatePlot();
    }

    // ============================================================
    // АВТОМАТИЧЕСКИЙ РАСЧЁТ
    // ============================================================

    private void TryAutoCalculate()
    {
      ResetFieldColors();

      if (string.IsNullOrWhiteSpace(_textBoxA.Text) ||
          string.IsNullOrWhiteSpace(_textBoxB.Text) ||
          string.IsNullOrWhiteSpace(_textBoxE.Text) ||
          string.IsNullOrWhiteSpace(_textBoxFormula.Text))
      {
        _solverResult = null;
        _parser = null;

        _labelResult.ForeColor = Color.DarkGreen;
        _labelResult.Text = "Результат";

        ClearResultTable();
        ShowResultTable();

        _formsPlot.Plot.Clear();
        _formsPlot.Plot.Title("");
        _formsPlot.Refresh();

        return;
      }

      if (!TryParseDouble(_textBoxA.Text, out _leftBound))
      { MarkInvalid(_textBoxA, true); ShowError("некорректное значение a."); return; }

      if (!TryParseDouble(_textBoxB.Text, out _rightBound))
      { MarkInvalid(_textBoxB, true); ShowError("некорректное значение b."); return; }

      if (!TryParseDouble(_textBoxE.Text, out _precision))
      { MarkInvalid(_textBoxE, true); ShowError("некорректное значение e."); return; }

      if (_leftBound >= _rightBound)
      {
        MarkInvalid(_textBoxA, true);
        MarkInvalid(_textBoxB, true);
        ShowError("должно выполняться a < b.");
        return;
      }

      if (_precision <= 0)
      { MarkInvalid(_textBoxE, true); ShowError("точность e должна быть больше 0."); return; }

      try
      {
        _parser = new FunctionParser(_textBoxFormula.Text);
        MarkInvalid(_textBoxFormula, false);
      }
      catch (Exception exception)
      {
        MarkInvalid(_textBoxFormula, true);
        ShowError("ошибка формулы: " + exception.Message);
        return;
      }

      try
      {
        _solverResult = GoldenSectionSolver.Solve(
            _parser.Evaluate, _leftBound, _rightBound, _precision, CurrentMode);

        ShowResult();
        UpdatePlot();
      }
      catch (Exception exception)
      {
        _solverResult = null;
        ShowError(exception.Message);
      }
    }

    // ============================================================
    // ПОКАЗ РЕЗУЛЬТАТА
    // ============================================================

    private void ShowResult()
    {
      if (_solverResult == null) return;

      UpdateResultLabels(_solverResult.Mode);

      if (!_solverResult.Success)
      {
        _labelResult.ForeColor = Color.DarkRed;
        _labelResult.Text = "Ошибка: " + _solverResult.Error;

        ClearResultTable();
        HideResultTable();
        return;
      }

      _labelResult.ForeColor = Color.DarkGreen;
      _labelResult.Text = "Результат";

      SetResultValues(
          FormatByPrecision(_solverResult.Point, _precision),
          _solverResult.Value.ToString("F8", CultureInfo.InvariantCulture),
          _solverResult.Iterations.ToString(CultureInfo.InvariantCulture),
          _precision.ToString("G", CultureInfo.InvariantCulture));

      ShowResultTable();
    }

    // ============================================================
    // ФОРМАТ ПОГРЕШНОСТИ
    // ============================================================

    private static string FormatByPrecision(double value, double eps)
    {
      int decimals = 0;
      double temp = Math.Abs(eps);

      while (temp < 1.0 && decimals < 15)
      {
        temp *= 10.0;
        decimals++;
      }

      if (decimals < 1) decimals = 1;

      return value.ToString("F" + decimals, CultureInfo.InvariantCulture);
    }

    // ============================================================
    // ПАРСИНГ ЧИСЕЛ
    // ============================================================

    private static bool TryParseDouble(string text, out double value)
    {
      text = (text ?? "").Trim().Replace(',', '.');

      return double.TryParse(
          text,
          NumberStyles.Float,
          CultureInfo.InvariantCulture,
          out value);
    }

    // ============================================================
    // ПОСТРОИТЬ ГРАФИК
    // ============================================================

    private void BuildGraphOnly()
    {
      ResetFieldColors();

      if (!TryParseDouble(_textBoxA.Text, out _leftBound))
      { MarkInvalid(_textBoxA, true); ShowError("некорректное значение a."); return; }

      if (!TryParseDouble(_textBoxB.Text, out _rightBound))
      { MarkInvalid(_textBoxB, true); ShowError("некорректное значение b."); return; }

      if (!TryParseDouble(_textBoxE.Text, out _precision))
      { MarkInvalid(_textBoxE, true); ShowError("некорректное значение e."); return; }

      if (_leftBound >= _rightBound)
      {
        MarkInvalid(_textBoxA, true);
        MarkInvalid(_textBoxB, true);
        ShowError("должно выполняться a < b.");
        return;
      }

      if (_precision <= 0)
      { MarkInvalid(_textBoxE, true); ShowError("точность e должна быть больше 0."); return; }

      if (string.IsNullOrWhiteSpace(_textBoxFormula.Text))
      { MarkInvalid(_textBoxFormula, true); ShowError("введите формулу."); return; }

      try
      {
        _parser = new FunctionParser(_textBoxFormula.Text);
        MarkInvalid(_textBoxFormula, false);
      }
      catch (Exception exception)
      {
        MarkInvalid(_textBoxFormula, true);
        ShowError("ошибка формулы: " + exception.Message);
        return;
      }

      UpdatePlot();
    }

    // ============================================================
    // ГРАФИК
    // ============================================================

    private void UpdatePlot()
    {
      _formsPlot.Plot.Clear();

      if (_parser == null)
      {
        _formsPlot.Plot.Title("График не построен");
        _formsPlot.Refresh();
        return;
      }

      double margin = (_rightBound - _leftBound) * 0.5;
      if (margin < 1e-9) margin = 1.0;

      double xMin = _leftBound - margin;
      double xMax = _rightBound + margin;

      const int sampleCount = 800;

      double[] xs = new double[sampleCount + 1];
      double[] ys = new double[sampleCount + 1];

      for (int i = 0; i <= sampleCount; i++)
      {
        double x = xMin + (xMax - xMin) * i / sampleCount;
        xs[i] = x;

        try
        {
          double y = _parser.Evaluate(x);
          ys[i] = (double.IsNaN(y) || double.IsInfinity(y)) ? double.NaN : y;
        }
        catch
        {
          ys[i] = double.NaN;
        }
      }

      // Разрыв линии в точках скачка (полюса)
      double[] deltas = new double[sampleCount];
      int deltaCount = 0;

      for (int i = 1; i <= sampleCount; i++)
      {
        if (!double.IsNaN(ys[i]) && !double.IsNaN(ys[i - 1]))
          deltas[deltaCount++] = Math.Abs(ys[i] - ys[i - 1]);
      }

      if (deltaCount > 0)
      {
        Array.Sort(deltas, 0, deltaCount);
        double medianDelta = deltas[deltaCount / 2];

        double maxAbsY = 0;
        for (int i = 0; i <= sampleCount; i++)
        {
          if (!double.IsNaN(ys[i]) && !double.IsInfinity(ys[i]))
          {
            double a = Math.Abs(ys[i]);
            if (a > maxAbsY) maxAbsY = a;
          }
        }

        double jumpThreshold = Math.Max(medianDelta * 20.0, maxAbsY * 0.5);
        if (jumpThreshold < 1e-9) jumpThreshold = 1e-9;

        for (int i = 1; i <= sampleCount; i++)
        {
          if (double.IsNaN(ys[i]) || double.IsNaN(ys[i - 1]))
            continue;

          if (Math.Abs(ys[i] - ys[i - 1]) > jumpThreshold)
            ys[i] = double.NaN;
        }
      }

      var scatter = _formsPlot.Plot.Add.Scatter(xs, ys);
      scatter.Color = ScottPlot.Color.FromSDColor(Color.SteelBlue);
      scatter.LineWidth = 2;
      scatter.MarkerSize = 0;

      var leftLine = _formsPlot.Plot.Add.VerticalLine(_leftBound);
      leftLine.Color = ScottPlot.Color.FromSDColor(Color.Orange).WithAlpha(0.6);
      leftLine.LinePattern = ScottPlot.LinePattern.Dashed;

      var rightLine = _formsPlot.Plot.Add.VerticalLine(_rightBound);
      rightLine.Color = ScottPlot.Color.FromSDColor(Color.Orange).WithAlpha(0.6);
      rightLine.LinePattern = ScottPlot.LinePattern.Dashed;

      // Финальный интервал локализации [left; right]
      if (_solverResult != null &&
          _solverResult.FinalIntervalRight > _solverResult.FinalIntervalLeft &&
          _solverResult.FinalIntervalRight - _solverResult.FinalIntervalLeft <
          (_rightBound - _leftBound) * 0.9)
      {
        var foundRange = _formsPlot.Plot.Add.HorizontalSpan(
            _solverResult.FinalIntervalLeft,
            _solverResult.FinalIntervalRight);

        foundRange.FillColor = ScottPlot.Color
            .FromSDColor(Color.Green).WithAlpha(0.2);
      }

      // Точка экстремума / корня
      if (_solverResult != null && _solverResult.Success)
      {
        var pointMarker = _formsPlot.Plot.Add.Marker(
            _solverResult.Point,
            _solverResult.Value);

        pointMarker.Color = ScottPlot.Color.FromSDColor(Color.Red);
        pointMarker.Size = 12;
      }

      string modeLabel =
          CurrentMode == OptimizationMode.Minimum ? "min" :
          CurrentMode == OptimizationMode.Maximum ? "max" : "root";

      _formsPlot.Plot.Title($"f(x) = {_textBoxFormula.Text}   ({modeLabel})");
      _formsPlot.Plot.Axes.AutoScale();
      _formsPlot.Refresh();
    }
  }
}