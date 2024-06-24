using System.Globalization;
using System.Windows.Data;
using Kurome.Core.Devices;

namespace Kurome.Ui.Converters;

public class PairStateToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PairState pairState)
        {
            return pairState switch
            {
                PairState.Paired => "Paired",
                PairState.Unpaired => "Unpaired",
                PairState.PairRequested => "Pair Requested",
                PairState.PairRequestedByPeer => "Incoming pair request",
                _ => "Unknown"
            };
        }

        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}