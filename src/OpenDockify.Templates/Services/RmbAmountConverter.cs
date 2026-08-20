using System.Text;

namespace OpenDockify.Templates.Services;

public sealed record RmbConversion(string? Text, string? Error)
{
    public static RmbConversion Success(string text)
    {
        return new(text, null);
    }

    public static RmbConversion Failure(string error)
    {
        return new(null, error);
    }
}

/// <summary>
/// Converts a monetary amount (yuan) to Chinese uppercase (中文大写).
///
/// NOTE: this is a temporary in-module implementation so the renderer can
/// satisfy the template-engine spec today. The canonical
/// <c>OpenDockify.Finance.AmountToChinese</c> lands with the
/// <c>finance-conversion</c> change, which will move this logic (and its unit
/// tests) there; the renderer will then delegate to Finance.
/// </summary>
public static class RmbAmountConverter
{
    private const string _digits = "零壹贰叁肆伍陆柒捌玖";
    private static readonly string[] _units = { "", "拾", "佰", "仟" };

    public static RmbConversion Convert(decimal amount)
    {
        if (amount < 0)
        {
            return RmbConversion.Failure("Amount must not be negative.");
        }

        var cents = Math.Round(amount * 100, 0, MidpointRounding.AwayFromZero);
        var yuan = (long)(cents / 100);
        var fen = (long)(cents % 100);

        if (yuan == 0 && fen == 0)
        {
            return RmbConversion.Success("零元整");
        }

        var jiao = fen / 10;
        var fenDigit = fen % 10;

        var sb = new StringBuilder();
        if (yuan > 0)
        {
            sb.Append(ConvertWholeYuan(yuan)).Append('元');
        }

        if (jiao > 0)
        {
            sb.Append(_digits[(int)jiao]).Append('角');
        }

        if (fenDigit > 0)
        {
            sb.Append(_digits[(int)fenDigit]).Append('分');
        }

        if (fen == 0)
        {
            sb.Append('整');
        }

        return RmbConversion.Success(sb.ToString());
    }

    /// <summary>Converts the whole-yuan part (0..long) with 亿/万 groups.</summary>
    private static string ConvertWholeYuan(long yuan)
    {
        var yi = yuan / 1_0000_0000L;
        var wan = (yuan / 1_0000L) % 1_0000L;
        var rest = yuan % 1_0000L;

        var sb = new StringBuilder();
        var started = false;
        var pendingZero = false;

        AppendGroup(yi, "亿");
        AppendGroup(wan, "万");
        AppendGroup(rest, null);

        return sb.ToString();

        void AppendGroup(long value, string? unit)
        {
            if (value == 0)
            {
                if (started)
                {
                    pendingZero = true;
                }

                return;
            }

            var text = ConvertGroup(value);
            if (started)
            {
                if (pendingZero || value < 1000)
                {
                    sb.Append('零');
                }

                pendingZero = false;
            }

            sb.Append(text);
            if (unit is not null)
            {
                sb.Append(unit);
            }

            started = true;
        }
    }

    /// <summary>Converts a 0..9999 group with zero-run suppression.</summary>
    private static string ConvertGroup(long value)
    {
        var sb = new StringBuilder();
        var zeroPending = false;

        for (var pos = 3; pos >= 0; pos--)
        {
            var divisor = 1L;
            for (var i = 0; i < pos; i++)
            {
                divisor *= 10;
            }

            var digit = value / divisor % 10;
            if (digit == 0)
            {
                if (sb.Length > 0)
                {
                    zeroPending = true;
                }
            }
            else
            {
                if (zeroPending)
                {
                    sb.Append('零');
                    zeroPending = false;
                }

                sb.Append(_digits[(int)digit]);
                sb.Append(_units[pos]);
            }
        }

        return sb.ToString();
    }
}
