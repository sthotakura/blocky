using System.Windows;
using System.Windows.Controls;

namespace Blocky.Controls;

public partial class TimePicker : UserControl
{
    public static readonly DependencyProperty SelectedTimeProperty =
        DependencyProperty.Register(
            nameof(SelectedTime),
            typeof(TimeSpan?),
            typeof(TimePicker),
            new PropertyMetadata(null, OnSelectedTimeChanged));

    public TimeSpan? SelectedTime
    {
        get => (TimeSpan?)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    public TimePicker()
    {
        InitializeComponent();
        InitializeTimeComponents();
    }

    void InitializeTimeComponents()
    {
        // Hours (0-23)
        for (var i = 0; i < 24; i++)
        {
            HourPicker.Items.Add(i.ToString("D2"));
        }

        // Minutes (00, 15, 30, 45)
        for (var i = 0; i < 60; i += 15)
        {
            MinutePicker.Items.Add(i.ToString("D2"));
        }

        // Default selection
        HourPicker.SelectedIndex = 9; // 9 AM
        MinutePicker.SelectedIndex = 0; // 00 minutes
    }

    static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (TimePicker)d;
        var newTime = (TimeSpan?)e.NewValue;

        if (newTime.HasValue)
        {
            picker.HourPicker.SelectedIndex = newTime.Value.Hours;
            picker.MinutePicker.SelectedIndex = newTime.Value.Minutes / 15;
        }
    }

    void TimeComponent_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (HourPicker.SelectedIndex >= 0 && MinutePicker.SelectedIndex >= 0)
        {
            var hours = HourPicker.SelectedIndex;
            var minutes = MinutePicker.SelectedIndex * 15;
            SelectedTime = new TimeSpan(hours, minutes, 0);
        }
    }
}