using DexterLib;
using JockerSoft.Media;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;
using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;
using System.Globalization;

namespace FrameExtractor {


    public class ShellViewModel : Caliburn.Micro.PropertyChangedBase, IShell
    {
        public System.Windows.Controls.Canvas CANVAS { get; set; }
        
        string _frameOffset;
        string _pathSave;
        string _pathLoad;
        string _info;
        string _tmpFolder;
        string _imgPath;
        string _imgName;
        double _canvasWidth;
        double _canvasHeight;
        int _pixelWidth;
        int _pixelHeight;
        int _sliderMax;
        int _rectTop;
        int _rectLeft;
        int _rectHeight;
        int _rectWidth;
        int _imgCounter;
        int _progress;
        int _mouseX;
        int _mouseY;
        bool _videoLoaded;
        bool _mouseDown;
        Brush _image;
        IMediaDet _mediaDet = null;
        _AMMediaType _mediaType;
        Matrix<double> _intrinsicCamera = null;
        Matrix<double> _distCoefs = null;

        public ShellViewModel()
        {
            _frameOffset = "Enter milliseconds";
            _pathSave = "Folder for frames";
            _pathLoad = "Video for extraction";
            _info = "Welcome to FrameCutter";
            _imgName = "frameImage";
            _image = new SolidColorBrush(Color.FromRgb(0,0,0));
            _sliderMax = 0;
            _tmpFolder = System.Environment.GetEnvironmentVariable("temp") + "\\";
            RectLeft += 10;
            RectTop += 10;
            RectWidth += 10;
            RectHeight += 10;
            _pixelHeight = 0;
            _pixelWidth = 0;
            _canvasWidth = 0;
            _canvasHeight = 0;
            _videoLoaded = false;
            _mouseDown = false;
            _imgCounter = 0;
            _progress = 0;
            _imgPath = String.Empty;
            Parameters = String.Empty;
                //< Canvas Background = "{Binding CanvasIMG}" Grid.RowSpan = "2" x: Name = "ImagePlane" HorizontalAlignment = "Left" Margin = "10,10,10,10" Width = "680" Height = "460" />
        }

        ~ShellViewModel()
        {
            if (_mediaDet != null)
                Marshal.ReleaseComObject(_mediaDet);
            if (_imgPath != String.Empty)
            {
                string directoryPath = _tmpFolder;
                var dir = new System.IO.DirectoryInfo(directoryPath);
                foreach (var file in dir.EnumerateFiles(_imgName + "*.*"))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception) { }
                }
            }
        }

        #region Properties
        public int PixelWidth
        {
            get { return _pixelWidth; }
            set {
                if (value < 0 || value > _canvasWidth)
                    return;
                _pixelWidth = value;
                NotifyOfPropertyChange(() => PixelWidth);
            }
        }

        public int PixelHeight
        {
            get { return _pixelHeight; }
            set {
                if (value < 0 || value > _canvasHeight)
                    return;
                _pixelHeight = value;
                NotifyOfPropertyChange(() => PixelHeight);
            }
        }

        public string FrameOffset
        {
            get { return _frameOffset; }
            set
            {
                _frameOffset = value;
                NotifyOfPropertyChange(() => FrameOffset);
            }
        }

        public string PathSave
        {
            get { return _pathSave; }
            set
            {
                _pathSave = value;
                NotifyOfPropertyChange(() => PathSave);
            }
        }

        public string PathLoad
        {
            get { return _pathLoad; }
            set
            {
                _pathLoad = value;
                NotifyOfPropertyChange(() => PathLoad);
            }
        }

        public Brush CanvasIMG
        {
            get { return _image; }
            set
            {
                _image = value;
                NotifyOfPropertyChange(() => CanvasIMG);
            }
        }

        public string InfoText
        {
            get { return _info; }
            set
            {
                _info =  value + "\n\n" + _info;
                NotifyOfPropertyChange(() => InfoText);
            }
        }

        public int SliderMax
        {
            get { return _sliderMax; }
            set
            {
                _sliderMax = value;
                NotifyOfPropertyChange(() => SliderMax);
            }
        }

        public string Parameters { get; set; }

        public int Progress
        {
            get { return _progress; }
            set
            {
                _progress = value;
                NotifyOfPropertyChange(() => Progress);
            }
        }

        public int RectTop {
            get
            {
                return _rectTop;
            }

            set
            {
                _rectTop = value;
                NotifyOfPropertyChange(() => RectTop);
            }
        }

        public int RectLeft {
            get
            {
                return _rectLeft;
            }

            set
            {
                _rectLeft = value;
                NotifyOfPropertyChange(() => RectLeft);
            }
        }

        public int RectWidth {
            get
            {
                return _rectWidth;
            }

            set
            {
                _rectWidth = value;
                NotifyOfPropertyChange(() => RectWidth);
            }
        }

        public int RectHeight {
            get
            {
                return _rectHeight;
            }

            set
            {
                _rectHeight = value;
                NotifyOfPropertyChange(() => RectHeight);
            }
        }

        public int SizeNxN { get; set; }
        #endregion

#region ui listener
        public void ChooseVideo()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "Video files (*.avi)|*.avi|Video files (*.wmv)|*.wmv|Video files (*.mpeg)|*.mpeg|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                PathLoad = openFileDialog1.FileName;

                if(loadVideo())
                    showVideoFrame(0);
            }
        }

        public void ChooseFolder()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    PathSave = fbd.SelectedPath;
                }
            }
        }

        public void SaveFrames()
        {
            if (!_videoLoaded)
            {
                InfoText = "Error: You have to load a video first";
                return;
            }
            if (!System.IO.Directory.Exists(PathSave))
            {
                InfoText = "Error: You have to select a folder to save the images";
                return;
            }
            if (_canvasHeight == 0 || _canvasWidth == 0)
            {
                InfoText = "Error: Set the region to cut out in the video";
                return;
            }
            
            int videoLength = (int)(_mediaDet.StreamLength*1000);
            double offset;
            if(!double.TryParse(_frameOffset, out offset) || (offset <= 0.0 || offset > videoLength))
            {
                InfoText = "Frame offset must be set in milliseconds between 0 and " + videoLength + ".";
                return;
            }

            if (SizeNxN < 0 || SizeNxN >= _canvasHeight)
            {
                InfoText = "Error: Max bounds for the size are from 0 to " + _canvasHeight;
                return;
            }

            double percentage = 0.0;
            int posX, posY, width, height;
            Progress = (int)(percentage * 100);
            for(double iFrame = 0.0; iFrame <= videoLength; iFrame += offset)
            {
                percentage = iFrame / videoLength;
                if (percentage > 1.0)
                    percentage = 1.0;

                System.Drawing.Bitmap frame = FrameGrabber.GetFrameFromVideo(PathLoad, percentage);
                posX = (int)(frame.Width * (RectLeft / _canvasWidth));  
                posY = (int)(frame.Height * (RectTop / _canvasHeight));
                width = (int)(frame.Width * (RectWidth / _canvasWidth));
                height = (int)(frame.Height * (RectHeight / _canvasHeight));

                frame = frame.Clone(new System.Drawing.Rectangle(new System.Drawing.Point(posX, posY), new System.Drawing.Size(width, height)), frame.PixelFormat);
                if(SizeNxN != 0)
                    frame = new System.Drawing.Bitmap(frame, new System.Drawing.Size(SizeNxN, SizeNxN));

                if (_intrinsicCamera != null && _distCoefs != null)
                {
                    Image<Rgb, Byte> FrameBuffer = new Image<Rgb, byte>(frame);
                    Image<Rgb, Byte> FrameResultBuffer = new Image<Rgb, byte>(frame.Size);

                    CvInvoke.Undistort(FrameBuffer, FrameResultBuffer, _intrinsicCamera, _distCoefs);
                    frame = FrameResultBuffer.ToBitmap();
                }

                frame.Save(@PathSave + "\\" + "frameExtractor" + iFrame + ".png", System.Drawing.Imaging.ImageFormat.Png);
                Progress = (int)(percentage * 100);
            }
            Progress = 100;
        }

        public void SliderValueChanged(object sender, EventArgs args)
        {
            if (!_videoLoaded)
                return;

            System.Windows.Controls.Slider slider = sender as System.Windows.Controls.Slider;
            double positionInVideo = (slider.Value / SliderMax);
            showVideoFrame(positionInVideo);
        }

        public void PixelHeightChanged(object sender)
        {
            System.Windows.Controls.TextBox tb = sender as System.Windows.Controls.TextBox;
            int height = 0;
            if (Int32.TryParse(tb.Text, out height))
                RectHeight = height;
        }

        public void PixelWidthChanged(object sender)
        {
            System.Windows.Controls.TextBox tb = sender as System.Windows.Controls.TextBox;
            int width = 0;
            if (Int32.TryParse(tb.Text, out width))
                RectWidth = width;

        }

        public void MouseDown(System.Windows.Controls.Canvas sender, System.Windows.Input.MouseEventArgs args)
        {
            _mouseDown = true;
            _mouseX = (int)args.GetPosition(sender).X;
            _mouseY = (int)args.GetPosition(sender).Y;
        }

        public void MouseUp(System.Windows.Controls.Canvas sender, System.Windows.Input.MouseEventArgs args)
        {
            _mouseDown = false;
        }

        public void MouseMove(System.Windows.Controls.Canvas sender, System.Windows.Input.MouseEventArgs args)
        {
            _canvasWidth = sender.Width;
            _canvasHeight = sender.Height;
            if (_mouseDown)
            {
                int offsetVert = (int)args.GetPosition(sender).X - _mouseX;
                int offsetHor = (int)args.GetPosition(sender).Y - _mouseY;
                if ((RectLeft + offsetVert) > 0 && (RectLeft + offsetVert + RectWidth) < _canvasWidth)
                    RectLeft += offsetVert;
                if ((RectTop + offsetHor) > 0 && (RectTop + offsetHor + RectHeight) < _canvasHeight)
                    RectTop += offsetHor;
                _mouseX = (int)args.GetPosition(sender).X;
                _mouseY = (int)args.GetPosition(sender).Y;
            }
        }

        public void MouseWheel(System.Windows.Controls.Canvas sender, System.Windows.Input.MouseWheelEventArgs args)
        {
            if(args.Delta > 0 && (sender.Width > RectWidth && sender.Height > RectHeight))
            {
                RectWidth++;
                PixelWidth = RectWidth;
                RectHeight++;
                PixelHeight = RectHeight;
            }
            else if(args.Delta < 0 && (0 < RectWidth && 0 < RectHeight))
            {
                RectWidth--;
                PixelWidth = RectWidth;
                RectHeight--;
                PixelHeight = RectHeight;
            }
        }

        public void ApplyParams()
        {
            getIntrinsic();
        }
        #endregion

        public bool loadVideo()
        {
            try
            {
                if (FrameGrabber.openVideoStream(PathLoad, out _mediaDet, out _mediaType))
                {
                    SliderMax = (int)_mediaDet.StreamLength;
                    InfoText = "Video (" + (int)_mediaDet.StreamLength + "sec.) successfully loaded.";
                    _videoLoaded = true;
                    return true;
                }
                return false;
            }
            catch (System.Exception e)
            {
                string errorCode = e.Message.Split(':')[1].Substring(3);
                uint errorValue = (uint)int.Parse(errorCode, System.Globalization.NumberStyles.HexNumber);
                InfoText = FrameGrabber.getErrorMsg(errorValue);
                return false;
            }
        }

        private void getIntrinsic()
        {
            if(Parameters == String.Empty || Parameters == "")
            {
                return;
            }

            string[] splitted = Parameters.Split(';');
            if(splitted.Length != 2)
            {
                InfoText = "Warning: Input parameters wrong. No distortion will be removed.";
                return;
            }

            string cameraStr = splitted[0];
            string coefsStr = splitted[1];

            string[] cameraParams = cameraStr.Split(',');
            string[] coefParams = coefsStr.Split(',');

            if (cameraParams.Length != 4 || coefParams.Length != 5)
            {
                InfoText = "Warning: Input parameters wrong. No distortion will be removed.";
                return;
            }

            try
            {
                Matrix<double> cameraMatrix = new Matrix<double>(3, 3);
                cameraMatrix.Data[0, 0] = double.Parse(cameraParams[0], CultureInfo.InvariantCulture);
                cameraMatrix.Data[0, 1] = 0;
                cameraMatrix.Data[0, 2] = double.Parse(cameraParams[1], CultureInfo.InvariantCulture);
                cameraMatrix.Data[1, 0] = 0;
                cameraMatrix.Data[1, 1] = double.Parse(cameraParams[2], CultureInfo.InvariantCulture);
                cameraMatrix.Data[1, 2] = double.Parse(cameraParams[3], CultureInfo.InvariantCulture);
                cameraMatrix.Data[2, 0] = 0;
                cameraMatrix.Data[2, 1] = 0;
                cameraMatrix.Data[2, 2] = 1;

                Matrix<double> disortionCoefs = new Matrix<double>(5, 1);
                disortionCoefs.Data[0, 0] = double.Parse(coefParams[0], CultureInfo.InvariantCulture);
                disortionCoefs.Data[1, 0] = double.Parse(coefParams[1], CultureInfo.InvariantCulture);
                disortionCoefs.Data[2, 0] = double.Parse(coefParams[2], CultureInfo.InvariantCulture);
                disortionCoefs.Data[3, 0] = double.Parse(coefParams[3], CultureInfo.InvariantCulture);
                disortionCoefs.Data[4, 0] = double.Parse(coefParams[4], CultureInfo.InvariantCulture);

                _intrinsicCamera = cameraMatrix;
                _distCoefs = disortionCoefs;

                InfoText = "Success: Valid parameters entered. Disortion will be removed.";
                return;
            }
            catch (Exception)
            {
                InfoText = "Warning: At least one entry was no valid number. No distortion will be removed";
                return;
            }

        }

        private void showVideoFrame(double part)
        {
            try
            {
                System.Drawing.Bitmap frame = FrameGrabber.GetFrameFromVideo(PathLoad, part);

                if(_intrinsicCamera != null && _distCoefs != null)
                {
                    Image<Rgb, Byte> FrameBuffer = new Image<Rgb, byte>(frame);
                    Image<Rgb, Byte> FrameResultBuffer = new Image<Rgb, byte>(frame.Size);

                    CvInvoke.Undistort(FrameBuffer, FrameResultBuffer, _intrinsicCamera, _distCoefs);
                    frame = FrameResultBuffer.ToBitmap();
                }
                
                _imgPath = _tmpFolder + _imgName + _imgCounter++ + ".png";
                frame.Save(@_imgPath, System.Drawing.Imaging.ImageFormat.Png);
                ImageBrush brush = new ImageBrush();
                brush.ImageSource = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(@_imgPath, System.UriKind.Absolute));
                CanvasIMG = brush;

            }
            catch(InvalidVideoFileException)
            {
                InfoText = "An error occured. Video file has the wrong format.";
            }
            catch (System.ArgumentOutOfRangeException)
            {
                InfoText = "An error occured. Video part doesn't exist.";
            }
        }
    }
}

//8.4121274227007063e+02, 640, 8.4121274227007063e+02, 480; -3.9667825864183698e-01, 1.2974231536748604e-01, 0, 0, -1.5871869359349713e-02