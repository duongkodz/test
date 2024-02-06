using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using ZXing;
using System.Threading;
namespace VISION_STATION_4
{
    public class ImageProcessing
    {
        public static OpenCvSharp.Point[] Vertex = new OpenCvSharp.Point[4];
        double pixel_cm_ratio;
        public Mat GetMat(System.Drawing.Image Image)
        {
            Bitmap bitmap = (Bitmap)Image;
            Mat PictureBoxImage = BitmapConverter.ToMat(bitmap);
            return PictureBoxImage;
        }
        #region Contour Detection
        public Mat[] ContourDetect(Mat Image, int MinArea, int MaxArea)
        {
            Mat CloneImage = Image.Clone();
            Cv2.CvtColor(CloneImage, CloneImage, ColorConversionCodes.BGR2GRAY);
            Mat BlurImage = CloneImage.GaussianBlur(new OpenCvSharp.Size(3, 3), 0);
            double MaxVal = 255;
            double Thresh = 127;
            Mat Convert_Image = new Mat();
            Cv2.Threshold(BlurImage, Convert_Image, Thresh, MaxVal, ThresholdTypes.Binary);
            Mat[] contours;
            Mat hierarchy = new Mat();
            Cv2.FindContours(Convert_Image, out contours, hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);
            Mat[] filteredContours = Array.FindAll(contours, c => (Cv2.ContourArea(c) > MinArea && Cv2.ContourArea(c) < MaxArea));
            return filteredContours;
        }
        #endregion
        #region DetectWorkpiece
        public void RectangleDetect(Mat Image)
        {
            Mat[] contours = ContourDetect(Image, 1000000, 2000000);
            var rotatedRect = Cv2.MinAreaRect(contours[0]);
            var boxPoints = Cv2.BoxPoints(rotatedRect);
            var box = boxPoints.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();

            Image.Polylines(new OpenCvSharp.Point[][] { box }, isClosed: true, color: Scalar.Red, thickness: 5);
            Cv2.Circle(Image, (int)rotatedRect.Center.X, (int)rotatedRect.Center.Y, 8, Scalar.Red, -1);
            Global_Var.Data_X = (int)((rotatedRect.Center.X - Global_Var.MasterPointX) * pixel_cm_ratio);
            Global_Var.Data_Y = (int)((rotatedRect.Center.Y - Global_Var.MasterPointY) * pixel_cm_ratio);
            Vertex = box;
        }
        #endregion
        #region QR
        public string QRReader(Bitmap bitmap)
        {
            BarcodeReader barcodeReader = new BarcodeReader();
            var result = barcodeReader.Decode(bitmap);
            string qrCodeContent = null;
            if (result != null)
            {
                qrCodeContent = result.Text;
            }
            else
                qrCodeContent = null;
            return qrCodeContent;
        }
        #endregion
        #region Contour
        public bool ContourLength(Mat Image, int MaxLength)
        {
            Mat[] ROIcontours = ContourDetect(Image, 1000000, 1500000);
            Rect rect = Cv2.BoundingRect(ROIcontours[0]);
            Rect RoiRect = new Rect(rect.Location, rect.Size);
            Mat ROIMat = new Mat(Image, RoiRect);
            Mat[] contours = ContourDetect(ROIMat, 60000, 90000);
            double Length = Cv2.ArcLength(contours[0], closed: true);
            if (Length >= MaxLength)  //max 7955, type1
            {
                return true;
            }
            else
                return false;
        }
        #endregion        
        #region screw check
        public Mat imageROI(Mat Image, OpenCvSharp.Point StartPoint, OpenCvSharp.Size Size)
        {
            Rect rect = new Rect(StartPoint, Size);
            Mat ROIMat = new Mat(Image, rect);
            Mat GrayMat = ROIMat.CvtColor(ColorConversionCodes.BGR2GRAY);
            return GrayMat;
        }
        public int LightPixelCheck(Mat Image, int val)
        {
            double MaxVal = 255;
            double Thresh = 100;
            Mat Convert_Image = new Mat();
            Cv2.Threshold(Image, Convert_Image, Thresh, MaxVal, ThresholdTypes.Binary);
            int a = Cv2.CountNonZero(Convert_Image);
            if (a > val)  
            {
                return 1;
            }
            else
                return 0;
        }
        public int[] CheckScrewType1(Mat Image)
        {
            int[] Check = new int[4];
            Mat matRoi1 = imageROI(Image, new OpenCvSharp.Point(306, 149), new OpenCvSharp.Size(66, 66));
            Mat matRoi3 = imageROI(Image, new OpenCvSharp.Point(835, 160), new OpenCvSharp.Size(66, 66));
            Mat matRoi5 = imageROI(Image, new OpenCvSharp.Point(830, 1275), new OpenCvSharp.Size(66, 66));
            Mat matRoi7 = imageROI(Image, new OpenCvSharp.Point(314, 1289), new OpenCvSharp.Size(66, 66));

            Check[0] = LightPixelCheck(matRoi1, 1800);
            Check[1] = LightPixelCheck(matRoi3, 1800);
            Check[2] = LightPixelCheck(matRoi5, 1500);
            Check[3] = LightPixelCheck(matRoi7, 2500);
            return Check;
        }
        public int[] CheckScrewType2(Mat Image)
        {
            int[] Check = new int[4];
            Mat matRoi2 = imageROI(Image, new OpenCvSharp.Point(575, 172), new OpenCvSharp.Size(66, 66));
            Mat matRoi4 = imageROI(Image, new OpenCvSharp.Point(820, 720), new OpenCvSharp.Size(66, 66));
            Mat matRoi6 = imageROI(Image, new OpenCvSharp.Point(576, 1263), new OpenCvSharp.Size(66, 66));
            Mat matRoi8 = imageROI(Image, new OpenCvSharp.Point(333, 716), new OpenCvSharp.Size(66, 66));

            Check[0] = LightPixelCheck(matRoi2, 2000);
            Check[1] = LightPixelCheck(matRoi4, 2000);
            Check[2] = LightPixelCheck(matRoi6, 2000);
            Check[3] = LightPixelCheck(matRoi8, 2000);
            return Check;
        }
        public int ScrewCount(Mat Image, int[] check)
        {
            int count = 0;
            if (Global_Var.type == 1)
            {
                check = CheckScrewType1(Image);
            }
            else
            {
                check = CheckScrewType2(Image);
            }
            for (int i = 0; i < 4; i++)
            {
                if (check[i] == 1)
                {
                    count++;
                }
            }
            return count;
        }
        #endregion

    }
}

