﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight;
using MvvmDialogs;
using MvvmDialogs.FrameworkPickers.FileSave;
using MvvmDialogs.FrameworkPickers.Folder;
using UwpGetImage.Classes;
using UwpGetImage.Models;
using CodaDevices.Uwp.Devices;
using System.Diagnostics;

namespace UwpGetImage.ViewModels
{
    public class MainPageViewModel : ViewModelBase
    {
        //Private vars.
        private readonly ClearShotCamera _camera;
        private readonly IDialogService _dialogService;
        private ushort[,] _lastImageArray;

        //Intiate.
        public MainPageViewModel(IDialogService dialogService)
        {
            //Setup dialog service.
            _dialogService = dialogService;

            //Setup cmds: 
            GetImageCommand = new RelayCommand(GetImageMethod);
            SaveImageCommand = new RelayCommand(SaveImageMethod);

            //Setup camera: 
            _camera = new ClearShotCamera();
            _camera.StartWaitForCamera();
            _camera.Connected += OnCameraConnected;
            _camera.Disconnected += OnCameraDisconnected;

            //Set page disabled.
            IsAppEnabled = false;

            //Set laser to be checked.
            IsLaserChecked = true;
            SaveAmount = 1;
        }

        #region Data needed from UI: 

        private async void CallOnMainThread(DispatchedHandler agileCallback)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, agileCallback);
        }

        private bool _isAppEnabled;
        public bool IsAppEnabled
        {
            get { return _isAppEnabled; }
            set
            {
                _isAppEnabled = value;
                CallOnMainThread(() => { RaisePropertyChanged("IsAppEnabled"); });
            }
        }
        private float _exposureTime;
        public float ExposureTime
        {
            get { return _exposureTime; }
            set
            {
                _exposureTime = value;
                CallOnMainThread(() => { RaisePropertyChanged("ExposureTime"); });
            }
        }
        private bool _isLive;
        public bool IsLiveChecked
        {
            get { return _isLive; }
            set
            {
                _isLive = value;
                CallOnMainThread(() => { RaisePropertyChanged("IsLiveChecked"); });
            }
        }
        private bool _isScaled;
        public bool IsScaledChecked
        {
            get { return _isScaled; }
            set
            {
                _isScaled = value;
                CallOnMainThread(() => { RaisePropertyChanged("IsScaledChecked"); });
            }
        }
        private bool _isLaser;
        public bool IsLaserChecked
        {
            get { return _isLaser; }
            set
            {
                _isLaser = value;
                CallOnMainThread(() => { RaisePropertyChanged("IsLaserChecked"); });
            }
        }
        private string _minValue;
        public string MinValue
        {
            get { return _minValue; }
            set
            {
                _minValue = value;
                CallOnMainThread(() => { RaisePropertyChanged("MinValue"); });
            }
        }
        private string _maxValue;
        public string MaxValue
        {
            get { return _maxValue; }
            set
            {
                _maxValue = value;
                CallOnMainThread(() => { RaisePropertyChanged("MaxValue"); });
            }
        }
        private int _saveAmount;
        public int SaveAmount
        {
            get { return _saveAmount; }
            set
            {
                _saveAmount = value;
                CallOnMainThread(() => { RaisePropertyChanged("SaveAmount"); });
            }
        }
        private WriteableBitmap _image;
        public WriteableBitmap Image
        {
            get { return _image; }
            set
            {
                _image = value;
                CallOnMainThread(() => { RaisePropertyChanged("Image"); });
            }
        }
        private bool _getImageButtonEnabled;
        public bool GetImageButtonEnabled
        {
            get { return _getImageButtonEnabled; }
            set
            {
                _getImageButtonEnabled = value;
                CallOnMainThread(() => { RaisePropertyChanged("GetImageButtonEnabled"); });
            }
        }
        private bool _saveImageButtonEnabled;
        public bool SaveImageButtonEnabled
        {
            get { return _saveImageButtonEnabled; }
            set
            {
                _saveImageButtonEnabled = value;
                CallOnMainThread(() => { RaisePropertyChanged("SaveImageButtonEnabled"); });
            }
        }

        private string _currentStatus;
        public string CurrentStatus
        {
            get { return _currentStatus; }
            set
            {
                _currentStatus = value;
                CallOnMainThread(() => { RaisePropertyChanged("CurrentStatus"); });
            }
        }
        //Commands: 
        public ICommand GetImageCommand { get; private set; }
        public ICommand SaveImageCommand { get; private set; }
        #endregion

        #region Methods
        async void GetImageMethod()
        {
            //Disable GetImageButton.
            GetImageButtonEnabled = false;
            SaveImageButtonEnabled = false;

            //Get Image.
            Image = await GetImageFromCamera();

            //Enable GetImageButton and SaveImage.
            GetImageButtonEnabled = true;
            SaveImageButtonEnabled = true;

            //If live checked, Redo.
            if(IsLiveChecked)
                GetImageMethod();
        }
        async void SaveImageMethod()
        {
            //Setup metadata.
            var metaData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(
                    "Format", "png"
                    ),
                new KeyValuePair<string, string>(
                    "Application Name", "Reconstruction and Calibration Tool (UWP)"
                    ),
                new KeyValuePair<string, string>(
                    "Camera Manufacturer", "Centice"
                    ),
                new KeyValuePair<string, string>(
                    "Exposure Time", ExposureTime.ToString()
                    ),
                new KeyValuePair<string, string>(
                    "Laser enabled", IsLaserChecked.ToString()
                    ),
                new KeyValuePair<string, string>(
                    "Values scaled", IsScaledChecked.ToString()
                    ),
                new KeyValuePair<string, string>(
                    "Min value", MinValue
                    ),
                new KeyValuePair<string, string>(
                    "Max value", MaxValue
                    )
            };

            if (SaveAmount == 1)
            {
                var saveFileSettings = new FileSavePickerSettings
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    FileTypeChoices = new Dictionary<string, IList<string>>
                    {
                      { "PNG Images", new List<string> { ".png" } }
                    },
                    DefaultFileExtension = ".png"
                };

                StorageFile storageFile = await _dialogService.PickSaveFileAsync(saveFileSettings);
                if (storageFile != null)
                {
                    StorageFile img = await Imaging.WriteableBitmapToStorageFile(_lastImageArray, IsScaledChecked, metaData);
                    await img.CopyAndReplaceAsync(storageFile);
                }
            }
            else
            {
                //Disable Buttons.
                SaveImageButtonEnabled = false;
                GetImageButtonEnabled = false;

                //Get folder to save imgs.
                var folderPickerSettings = new FolderPickerSettings
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    FileTypeFilter = new List<string> { ".png" }
                };
                StorageFolder storageFolder = await _dialogService.PickSingleFolderAsync(folderPickerSettings);

                //GetImage, Save, Loop.
                for (uint i = 0; i < SaveAmount; i++)
                {
                    try
                    {
                        Image = await GetImageFromCamera();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }

                    StorageFile img = await Imaging.WriteableBitmapToStorageFile(_lastImageArray, IsScaledChecked, metaData);
                    string strDateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss-");
                    await img.CopyAsync(storageFolder, strDateTime + "img" + i + ".png");
                }
                SaveImageButtonEnabled = true;
                GetImageButtonEnabled = true;
            }
        }
        
        async Task<WriteableBitmap> GetImageFromCamera()
        {
            CurrentStatus = "Checking for laser.";
            bool isLaserAvailable = await _camera.IsLaserAvailable();
            if (isLaserAvailable && IsLaserChecked)
            {
                CurrentStatus = "Starting Laser";
                await _camera.SetLaserEnabled(1, true);
            }

            CurrentStatus = "Setting exposure time.";
            await _camera.SetExposureTime(ExposureTime);

            //Start taking image. [shutterState = open, exposureType = light]
            CurrentStatus = "Starting exposure.";
            if (await _camera.StartExposure(0x01, 0x01))
            {
                var currentTime = DateTime.Now;
                Await:
                //Await the exposure time.
                await Funcs.PutTaskDelay(1000); //Just let it wait a second before a 

                //Make sure image is ready.
                CurrentStatus = $"Checking for exposure, elapsed {DateTime.Now.Subtract(currentTime).Seconds} seconds.";
                if (await _camera.QueryExposure())
                {
                    //Get the image.
                    CurrentStatus = "Exposure available, now getting image.";
                    ushort[,] img = await _camera.GetExposure();

                    //Stop the exposure.
                    CurrentStatus = "Ending Exposure.";
                    await _camera.EndExposure(0x00); //Close shutter.

                    //Now stop the laser
                    if (isLaserAvailable && IsLaserChecked)
                    {
                        CurrentStatus = "Stoping Laser.";
                        await _camera.SetLaserEnabled(1, false);
                    }

                    //Quickly set the textbox data.
                    CurrentStatus = "Setting Min and Max values.";
                    var imgHeight = img.GetLength(0);
                    var imgWidth = img.GetLength(1);
                    var maxVal = UInt16.MinValue;
                    var minVal = UInt16.MaxValue;
                    for (int i = 10; i < imgHeight; i++)
                    {
                        for (int j = 10; j < imgWidth; j++)
                        {
                            if (maxVal < img[i, j]) maxVal = img[i, j];
                            if (minVal > img[i, j]) minVal = img[i, j];
                        }
                    }
                    MaxValue = maxVal + "(" + (maxVal * 100 / UInt16.MaxValue).ToString() + "%)";
                    MinValue = minVal + "(" + (minVal * 100 / UInt16.MaxValue).ToString() + "%)";

                    //Now convert the image and return.
                    CurrentStatus = "Done";
                    _lastImageArray = img;
                    return Imaging.GetImageFromUShort(img, IsScaledChecked, false);
                }
                else
                {
                    goto Await;
                }
            }
            return null;
        }
        #endregion

        #region Events
        async void OnCameraConnected(object sender, EventArgs e)
        {
            IsAppEnabled = true; GetImageButtonEnabled = true;
            ToastNotificationString notification = new ToastNotificationString("Connected!",
                "The camera has been connected.", DateTimeOffset.Now.AddMinutes(1));
            notification.Show();

            string strSerialNumber = await _camera.GetSerialNumber();
            string strCalibration = await _camera.GetCalibration();
            string fileName = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            fileName += "-Calibration" + strSerialNumber + ".xml";
            try
            {
                StorageFile file = await KnownFolders.SavedPictures.CreateFileAsync(fileName);
                await FileIO.WriteTextAsync(file, strCalibration);
            }
            catch (UnauthorizedAccessException e1)
            {
                Debug.WriteLine("Failed to create file \"" + fileName + "\": " + e1.Message);
            }
            catch (Exception e2)
            {
                Debug.WriteLine("Exception: " + e2.Message);
            }
            //System.IO.File.WriteAllText(fileName, strCalibration);
            /*if (!string.IsNullOrEmpty(strCalibration))
            {
                DeviceCalibration = XmlSerializationUtil.FromXml<SpectrometerCalibration>(strCalibration);
            }*/

            //Set default.
            ExposureTime = await _camera.GetExposureTime();
        }

        void OnCameraDisconnected(object sender, EventArgs e)
        {
            IsAppEnabled = false;
            ToastNotificationString notification = new ToastNotificationString("Disconnected!",
                "The camera has been disconnected.", DateTimeOffset.Now.AddMinutes(1));
            notification.Show();
        }
#endregion

    }
}