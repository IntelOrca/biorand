using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace IntelOrca.Biohazard.BioRand
{
    /// <summary>
    /// Interaction logic for CheckBoxList.xaml
    /// </summary>
    public partial class CheckBoxList : UserControl
    {
        private readonly Rng _random = new Rng();
        private CheckBoxListItem[] _items = new CheckBoxListItem[0];
        private bool _suspendEvents;

        public event RoutedEventHandler ItemValueChanged;

        public int Count => _items.Length;

        public CheckBoxList()
        {
            InitializeComponent();
        }

        public string[] Names
        {
            get => _items.Select(x => x.Text).ToArray();
            set
            {
                _items = value
                    .Select(x => new CheckBoxListItem(x, true))
                    .ToArray();
                list.ItemsSource = _items;
            }
        }

        public object[] ToolTips
        {
            get => _items.Select(x => x.Text).ToArray();
            set
            {
                for (var i = 0; i < _items.Length; i++)
                {
                    if (value.Length <= i)
                        break;

                    _items[i].ToolTip = value[i];
                }
            }
        }

        public bool[] Values
        {
            get => _items.Select(x => x.IsChecked).ToArray();
            set
            {
                for (var i = 0; i < Count; i++)
                {
                    SetItemChecked(i, value.Length > i && value[i]);
                }
            }
        }

        public void SetItemValues(bool[] values)
        {
            for (var i = 0; i < Count; i++)
            {
                var value = values.Length > i ? values[i] : false;
                SetItemChecked(i, value);
            }
        }

        public void SetItemChecked(int index, bool value)
        {
            if (index >= 0 && index < _items.Length)
            {
                _items[index].IsChecked = value;
            }
        }

        private void BulkModify(Action modifyLogic)
        {
            try
            {
                _suspendEvents = true;
                modifyLogic();
            }
            finally
            {
                _suspendEvents = false;
            }
            RaiseChangeEvent();
        }

        private void RaiseChangeEvent()
        {
            if (!_suspendEvents)
            {
                ItemValueChanged?.Invoke(this, new RoutedEventArgs());
            }
        }

        private void menuUnselectAll_Click(object sender, RoutedEventArgs e)
        {
            BulkModify(() =>
            {
                foreach (CheckBoxListItem item in list.ItemsSource)
                {
                    item.IsChecked = false;
                }
            });
        }

        private void menuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            BulkModify(() =>
            {
                foreach (CheckBoxListItem item in list.ItemsSource)
                {
                    item.IsChecked = true;
                }
            });
        }

        private void menuRandom_Click(object sender, RoutedEventArgs e)
        {
            BulkModify(() =>
            {
                var items = list.ItemsSource.Cast<CheckBoxListItem>();
                var numItems = items.Count();
                var numChecked = _random.Next(1, numItems);
                var checkedItems = items.Shuffle(_random).Take(numChecked).ToArray();
                foreach (CheckBoxListItem item in list.ItemsSource)
                {
                    item.IsChecked = checkedItems.Contains(item);
                }
            });
        }

        private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            RaiseChangeEvent();
        }
    }

    public class CheckBoxListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _text;
        private object _toolTip;
        private bool _isChecked;

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public object ToolTip
        {
            get => _toolTip;
            set
            {
                if (_toolTip != value)
                {
                    _toolTip = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTip)));
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public CheckBoxListItem()
        {
        }

        public CheckBoxListItem(string text, bool isChecked)
        {
            _text = text;
            _isChecked = isChecked;
        }
    }
}
