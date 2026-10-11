using System;

namespace GoldenSectionMethod
{
  public enum OptimizationMode
  {
    Minimum,
    Maximum,
    Root
  }

  public class GoldenSectionResult
  {
    public double Point;              // x* — точка экстремума или корень
    public double Value;              // f(x*) — значение ИСХОДНОЙ функции
    public int Iterations;
    public bool Success;
    public string Error;
    public string Warning;            // не блокирующее предупреждение
    public double FinalIntervalLeft;  // финальный [left; right] локализации
    public double FinalIntervalRight;
    public OptimizationMode Mode;
  }

  public static class GoldenSectionSolver
  {
    // 1/φ = (√5 − 1) / 2 ≈ 0.6180339887498949
    private const double InvPhi = 0.6180339887498949;
    private const int MaxIterations = 10000;

    public static GoldenSectionResult Solve(
        Func<double, double> function,
        double a, double b, double eps,
        OptimizationMode mode)
    {
      var result = new GoldenSectionResult { Mode = mode };

      if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(eps))
      { result.Error = "Входные значения не числа"; return result; }

      if (a >= b) { result.Error = "Требуется a < b"; return result; }
      if (eps <= 0) { result.Error = "Точность e должна быть > 0"; return result; }
      if (eps >= (b - a)) { result.Error = "Точность e больше длины интервала"; return result; }

      // Выбор целевой функции по режиму
      Func<double, double> target;
      switch (mode)
      {
        case OptimizationMode.Minimum: target = function; break;
        case OptimizationMode.Maximum: target = x => -function(x); break;
        case OptimizationMode.Root: target = x => Math.Abs(function(x)); break;
        default: result.Error = "Неизвестный режим поиска"; return result;
      }

      // Проверка вычислимости на границах
      double tmp;
      if (!TryEvaluate(target, a, out tmp) || !TryEvaluate(target, b, out tmp))
      {
        result.Error = "Функция не вычислима на границах интервала";
        return result;
      }

      // ===== Предварительная проверка унимодальности =====
      // Только для min/max (для корня понятие унимодальности |f| неприменимо).
      if (mode == OptimizationMode.Minimum || mode == OptimizationMode.Maximum)
      {
        result.Warning = CheckUnimodality(function, a, b, mode);
      }

      // Начальные точки золотого сечения
      double left = a, right = b;
      double x1 = right - InvPhi * (right - left);   // ближе к a
      double x2 = left + InvPhi * (right - left);    // ближе к b

      double f1, f2;
      if (!TryEvaluate(target, x1, out f1) || !TryEvaluate(target, x2, out f2))
      {
        result.Error = "Функция не вычислима внутри интервала";
        return result;
      }

      int iter = 0;

      while ((right - left) > eps && iter < MaxIterations)
      {
        iter++;

        if (f1 < f2)
        {
          // Минимум в [left; x2]. x1 становится новой x2.
          right = x2;
          x2 = x1; f2 = f1;
          x1 = right - InvPhi * (right - left);

          if (!TryEvaluate(target, x1, out f1))
          {
            result.Error = "Функция не вычислима внутри интервала (возможно, разрыв)";
            return result;
          }
        }
        else
        {
          // Минимум в [x1; right]. x2 становится новой x1.
          left = x1;
          x1 = x2; f1 = f2;
          x2 = left + InvPhi * (right - left);

          if (!TryEvaluate(target, x2, out f2))
          {
            result.Error = "Функция не вычислима внутри интервала (возможно, разрыв)";
            return result;
          }
        }
      }

      if (iter >= MaxIterations)
      {
        result.Error = "Превышено максимальное число итераций";
        return result;
      }

      double point = (left + right) * 0.5;

      // Значение исходной функции (не target) в найденной точке
      double fPoint;
      if (!TryEvaluate(function, point, out fPoint))
      {
        result.Error = "Функция не вычислима в найденной точке";
        return result;
      }

      result.Point = point;
      result.Value = fPoint;
      result.Iterations = iter;
      result.FinalIntervalLeft = left;
      result.FinalIntervalRight = right;

      // ===== Проверки по режиму =====
      if (mode == OptimizationMode.Root)
      {
        // Минимум |f| должен быть близок к нулю
        double tolerance = Math.Max(1e-6, eps);
        if (Math.Abs(fPoint) > tolerance)
        {
          result.Error =
              $"Минимум |f(x)| = {Math.Abs(fPoint):G4} не близок к нулю. " +
              $"Корень не найден на [{a:G6}; {b:G6}].";
          return result;
        }
      }
      else
      {
        // Экстремум не должен лежать на границе —
        // иначе функция, скорее всего, монотонна и экстремума внутри нет.
        double edgeTol = Math.Max(eps * 1.5, 1e-12);
        if ((point - a) < edgeTol || (b - point) < edgeTol)
        {
          result.Error = "Экстремум на границе интервала. Расширьте [a; b].";
          return result;
        }
      }

      result.Success = true;
      return result;
    }

    // ============================================================
    // ПРОВЕРКА УНИМОДАЛЬНОСТИ
    // ============================================================
    // Численно считаем f'(x) в N точках [a; b] через центральную
    // разность и считаем смены знака.
    //   - 1 смена знака — унимодальна (норма).
    //   - 0 смен — монотонна, экстремум на границе.
    //   - >1 смен — неунимодальна, экстремум может быть локальным.
    // Возвращает null, если всё нормально, иначе — текст предупреждения.
    private static string CheckUnimodality(
        Func<double, double> f, double a, double b, OptimizationMode mode)
    {
      const int samples = 200;
      double h = Math.Max((b - a) * 1e-6, 1e-9);

      int signChanges = 0;
      int previousSign = 0;
      int validPoints = 0;

      for (int i = 0; i <= samples; i++)
      {
        double x = a + (b - a) * i / samples;
        double xPlus = x + h;
        double xMinus = x - h;
        if (xPlus > b || xMinus < a) continue;

        double fPlus, fMinus;
        if (!TryEvaluate(f, xPlus, out fPlus)) continue;
        if (!TryEvaluate(f, xMinus, out fMinus)) continue;

        double derivative = (fPlus - fMinus) / (2.0 * h);
        if (Math.Abs(derivative) < 1e-12) continue;

        validPoints++;
        int sign = Math.Sign(derivative);
        if (previousSign != 0 && sign != previousSign)
          signChanges++;

        previousSign = sign;
      }

      // Если не набрали достаточно точек — не гадаем
      if (validPoints < samples / 4) return null;

      string kind = (mode == OptimizationMode.Minimum) ? "минимум" : "максимум";

      if (signChanges > 1)
      {
        return $"Функция, возможно, неунимодальна: производная меняет знак {signChanges} раз(а). " +
               $"Найденный {kind} может быть локальным, а не глобальным.";
      }
      if (signChanges == 0)
      {
        return $"Функция, похоже, монотонна на отрезке. Внутреннего {kind}а может не быть — " +
               $"{kind} окажется на границе [a; b].";
      }

      return null; // всё ок
    }

    private static bool TryEvaluate(Func<double, double> f, double x, out double v)
    {
      try
      {
        v = f(x);
        return !double.IsNaN(v) && !double.IsInfinity(v);
      }
      catch
      {
        v = double.NaN;
        return false;
      }
    }
  }
}