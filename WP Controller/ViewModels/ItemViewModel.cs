using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WP_Controller.ViewModels
{
    public class ItemViewModel : INotifyPropertyChanged
    {
        private string _deviceId;
        public string DeviceId
        {
            get
            {
                return _deviceId;
            }
            set
            {
                _deviceId = value;
                NotifyPropertyChanged("DeviceId");
            }
        }

        private string _networkId;
        public string NetworkId
        {
            get
            {
                return _networkId;
            }
            set
            {
                _networkId = value;
                NotifyPropertyChanged("NetworkId");
            }
        }

        private string _uniqueAddress;
        public string UniqueAddress
        {
            get
            {
                return _uniqueAddress;
            }
            set
            {
                _uniqueAddress = value;
                NotifyPropertyChanged("UniqueAddress");
            }
        }

        private string _friendlyName;
        public string FriendlyName
        {
            get
            {
                return _friendlyName;
            }
            set
            {
                _friendlyName = value;
                NotifyPropertyChanged("FriendlyName");
            }
        }

        private DateTime _lastHeartBeat;
        public DateTime LastHeartBeat
        {
            get
            {
                return _lastHeartBeat;
            }
            set
            {
                _lastHeartBeat = value;
                NotifyPropertyChanged("LastHeartBeat");
            }
        }

        private DeviceType _deviceType;
        public DeviceType DeviceType
        {
            get
            {
                return _deviceType;
            }
            set
            {
                _deviceType = value;
                NotifyPropertyChanged("DeviceType");
            }
        }

        public string LineOne
        {
            get
            {
                string heading;
                if (_friendlyName != null && _friendlyName.Length > 0)
                {
                    heading = _networkId + ":" + _deviceId + " - " + _friendlyName;
                }
                else
                {
                    heading = _networkId + ":" + _deviceId;
                }

                return heading;
            }
        }

        public string LineTwo
        {
            get
            {
                return _uniqueAddress + " - " + _lastHeartBeat.ToString();
            }
        }

        private string _lineThree;
        /// <summary>
        /// Sample ViewModel property; this property is used in the view to display its value using a Binding.
        /// </summary>
        /// <returns></returns>
        public string LineThree
        {
            get
            {
                return _lineThree;
            }
            set
            {
                if (value != _lineThree)
                {
                    _lineThree = value;
                    NotifyPropertyChanged("LineThree");
                }
            }
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