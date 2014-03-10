using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using WP_Controller.Resources;

namespace WP_Controller.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _itemListLock = new object();
        public MainViewModel()
        {
            this.Items = new ObservableCollection<ItemViewModel>();
            this.Settings = new ObservableCollection<SettingViewModel>();
        }

        /// <summary>
        /// A collection for ItemViewModel objects.
        /// </summary>
        public ObservableCollection<ItemViewModel> Items { get; private set; }
        public ObservableCollection<SettingViewModel> Settings { get; private set; }

        public void AddOrUpdateDevice(ItemViewModel device)
        {
            ItemViewModel itemFound = null;

            lock (_itemListLock)
            {
                foreach (ItemViewModel item in Items.Where(a => a.DeviceId == device.DeviceId && a.NetworkId == device.NetworkId))
                {
                    itemFound = item;
                }
            }

            lock (_itemListLock)
            {
                // Remove default item
                if (this.Items.Count == 1)
                {
                    if (this.Items[0].DeviceId == "FF" && this.Items[0].NetworkId == "FF")
                    {
                        this.Items.RemoveAt(0);
                    }
                }

                device.LastHeartBeat = DateTime.Now;

                int indexOfItem = 0;
                if (itemFound != null)
                {
                    indexOfItem = this.Items.IndexOf(itemFound);
                    this.Items.Remove(itemFound);
                }
                this.Items.Insert(indexOfItem, device);
            }
        }

        //private string _sampleProperty = "Sample Runtime Property Value";
        ///// <summary>
        ///// Sample ViewModel property; this property is used in the view to display its value using a Binding
        ///// </summary>
        ///// <returns></returns>
        //public string SampleProperty
        //{
        //    get
        //    {
        //        return _sampleProperty;
        //    }
        //    set
        //    {
        //        if (value != _sampleProperty)
        //        {
        //            _sampleProperty = value;
        //            NotifyPropertyChanged("SampleProperty");
        //        }
        //    }
        //}

        ///// <summary>
        ///// Sample property that returns a localized string
        ///// </summary>
        //public string LocalizedSampleProperty
        //{
        //    get
        //    {
        //        return AppResources.SampleProperty;
        //    }
        //}

        public bool IsDataLoaded
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates and adds a few ItemViewModel objects into the Items collection.
        /// </summary>
        public void LoadData()
        {
            this.Settings.Add(new SettingViewModel() { LineOne = "connect to device", LineTwo = "forces a device reconnection over bluetooth" }); 
            this.Settings.Add(new SettingViewModel() { LineOne = "bluetooth setup", LineTwo = "modify default bluetooth settings" }); 

            // Sample data; replace with real data
            this.Items.Add(new ItemViewModel() {  NetworkId = "FF", DeviceId = "FF", FriendlyName = "No devices found", LastHeartBeat = DateTime.MinValue});
            
            this.IsDataLoaded = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}