using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;
using System.Windows.Forms;
using Sentech.GenApiDotNET;
namespace VISION_STATION_1
{
    public class Camera
    {
        #region Checking Position on Image
        static Rect BoundingRect = new Rect();
        static float MasterX = 0;
        static float MasterY = 0;
        static float MasterTheta = 0;
        public static void GetImage(System.Drawing.Image Image)
        {
            Bitmap bitmap = (Bitmap)Image;
            Mat PictureBoxImage = BitmapConverter.ToMat(bitmap);
            Global_Var.MatRef = PictureBoxImage;
        }
        public static Mat PreProcessing(Mat image, int k)
        {
            Mat CloneImage = image.Clone();
            Cv2.CvtColor(CloneImage, CloneImage, ColorConversionCodes.BGR2GRAY);
            Mat BlurImage = CloneImage.GaussianBlur(new OpenCvSharp.Size(k, k), 0);
            /*Using threshold to make binary image*/
            double MaxVal = 255;
            double Thresh = 127;
            Mat Convert_Image = new Mat();
            Cv2.Threshold(BlurImage, Convert_Image, Thresh, MaxVal, ThresholdTypes.Binary);
            return Convert_Image;
        }
        public static Mat[] ContourDetect(Mat image, double minContourArea, double maxContourArea)
        {
            Mat[] contours;
            Mat hierarchy = new Mat();
            image = PreProcessing(image, 15);
            image.FindContours(out contours, hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxNone);
            Mat[] filteredContours = Array.FindAll(contours, c => (Cv2.ContourArea(c) > minContourArea && Cv2.ContourArea(c) < maxContourArea));
            return filteredContours;
        }
        public static bool FindPosition(Mat[] contours)
        {
            if (contours.Length > 0)
            {
                BoundingRect = Cv2.BoundingRect(contours[0]);
                return true;
            }
            else 
                return false;
        }
        public static RotatedRect RoiProcess(Mat image,Mat contours)
        {
            var rotatedRect = Cv2.MinAreaRect(contours);
            var boxPoints = Cv2.BoxPoints(rotatedRect);
            var box = boxPoints.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
            image.Polylines(new OpenCvSharp.Point[][] { box }, isClosed: true, color: Scalar.Red, thickness: 5);
            Cv2.Circle(image, (int)rotatedRect.Center.X, (int)rotatedRect.Center.Y, 8, Scalar.Red, -1);
            return rotatedRect;
            
        }
        public static float[] PositionData(RotatedRect rotatedRect)
        {
            float[] Data = new float[3];
            //float PixelX = Data[0] 
            //float PixelY = Data[1] 
            //float PixelTheta = Data[1] 
            
            Data[0] = MasterX- rotatedRect.Center.X;
            Data[1] = MasterY- rotatedRect.Center.Y;
            Data[2] = MasterTheta+ rotatedRect.Angle;
            MessageBox.Show(Data[0].ToString() + "//" + Data[1].ToString() + "//" + Data[2].ToString());
            return Data;
        }
        public bool ImageProcessing()
        {
            GetImage(Global_Var.picRef);
            Mat[] contours =ContourDetect(Global_Var.MatRef, 1000, 3145728);
            if(FindPosition(contours)==true)
            {
                var rotatedRect = RoiProcess(Global_Var.MatRef, contours[0]);
                PositionData(rotatedRect);
                return true;
            }    
            else { return false; }
        }
        #endregion
        #region QR checking 
        public static Mat[] QRCodeProcessing(Mat image,RotatedRect rotatedRect)
        {

            Mat CloneImage = image.Clone();
            PreProcessing(CloneImage, 3);
            float[] Data = PositionData(rotatedRect);
            ///////////////////////////////////////////////////////////////////////
            OpenCvSharp.Point[] CenterPoint_ver = new OpenCvSharp.Point[2];

            double calibValue = 0.183;
            double RealDistance = 50;
            double pixelDistance = RealDistance / calibValue;
            int rectangleWidth = 70;
            int rectangleHeight = 40;
            if (Data[2] > 45)  
            {
                CenterPoint_ver[0] = new OpenCvSharp.Point((float)(Data[0] - pixelDistance * (float)Math.Asin(90 - Data[2])), (float)(Data[1] - pixelDistance * (float)Math.Acos(90 - Data[2])));
                CenterPoint_ver[1] = new OpenCvSharp.Point((float)(Data[0] + pixelDistance * (float)Math.Asin(90 - Data[2])), (float)(Data[1] + pixelDistance * (float)Math.Acos(90 - Data[2])));
            }
            else
            {
                CenterPoint_ver[0] = new OpenCvSharp.Point((float)(Data[0] + pixelDistance * (float)Math.Asin(90 - Data[2])), (float)(Data[1] - pixelDistance * (float)Math.Acos(90 - Data[2])));
                CenterPoint_ver[1] = new OpenCvSharp.Point((float)(Data[0] - pixelDistance * (float)Math.Asin(90 - Data[2])), (float)(Data[1] + pixelDistance * (float)Math.Acos(90 - Data[2])));
            }
            Mat[] croppedImage_Clone = new Mat[2];
            for (int i = 0; i < 2; i++)
            {
                Mat rotationMatrix = new Mat();
                if (Data[2] > 45)
                {
                    rotationMatrix = Cv2.GetRotationMatrix2D(new Point2f(CenterPoint_ver[i].X, CenterPoint_ver[i].Y), -(90 - Data[2]), 1.0);
                }
                else
                {
                    rotationMatrix = Cv2.GetRotationMatrix2D(new Point2f(CenterPoint_ver[i].X, CenterPoint_ver[i].Y), Data[2], 1.0);
                }
                Mat rotatedImage = new Mat();

                // Xoay ảnh
                Cv2.WarpAffine(CloneImage, rotatedImage, rotationMatrix, CloneImage.Size());

                int x = CenterPoint_ver[i].X - rectangleWidth / 2;
                int y = CenterPoint_ver[i].Y - rectangleHeight / 2;
                Rect roi = new Rect(x, y, rectangleWidth, rectangleHeight);
                Mat croppedImage = new Mat(rotatedImage, roi);

                croppedImage_Clone[i] = croppedImage.Clone();
            }

            return croppedImage_Clone;
        }
        public static int RoiCompare(Mat image, RotatedRect rotatedRect)
        {
            Mat CloneImage = image.Clone();
            Mat[] mat_clone = new Mat[2];
            mat_clone = QRCodeProcessing(image, rotatedRect);
            Mat Darkmat0 = PreProcessing(mat_clone[0], 3);
            Mat Darkmat1 = PreProcessing(mat_clone[1], 3);

            int a = Cv2.CountNonZero(Darkmat0);
            int b = Cv2.CountNonZero(Darkmat1);
            long c = Darkmat0.Total();

            if (a > b && a == c) 
            {
                return 1;
            }
            if (a < b && b == c) 
            {
                return 2;
            }
            else
                return 3;
        }
        #endregion
        #region  Calculate Real Point
        public static void OffsetToRobot(float[] Data)
        {
            /*Calculate deltaX and deltaY*/
            //float deltaX = Data[0] ;
            //float deltaY = Data[1] ;
            /*Convert Pixel to MM*/
            Global_Var.RealCenter.X = Data[0] * 0.2131 * 1000;
            Global_Var.RealCenter.Y = Data[1] * 0.2082 * 1000;
            Global_Var.RealAngle = Data[2] * 10000;
        }
        #endregion
    }
}
