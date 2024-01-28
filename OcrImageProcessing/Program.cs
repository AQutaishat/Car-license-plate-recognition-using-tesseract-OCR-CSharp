using OpenCvSharp;
using System;
using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Runtime.Intrinsics;
using TesseractOCR.Enums;
using TesseractOCR;
using static OpenCvSharp.Stitcher;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using Rect = OpenCvSharp.Rect;
using System.Text.RegularExpressions;

namespace OcrImageProcessing
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            TestProcessImage(@"carImage\8.jpg");
            //TestProcessImage(@"D:\temp\__to be deleted\1.png");
        }

        static void TestProcessImage(string ImagePath)
        {
            var image = Cv2.ImRead(ImagePath);
            currentFileNameExtension = Path.GetExtension(ImagePath);

            imshow("1-Original", image);
            var original_width = image.Cols;
            var original_height = image.Rows;
            //-------------------------------------------------
            // Image processing for contours
            var image2 = image.Clone();
            var image3 = image.Clone();
            var image4 = image.Clone();
            Cv2.CvtColor(image2, image2, ColorConversionCodes.BGR2GRAY);
            imshow("2-Original-to-Gray", image2);
            Cv2.GaussianBlur(image2, image2, new Size(5, 5), 0);
            imshow("3-Gray-to-GausianBlur", image2);
            Cv2.Canny(image2, image2, 100, 300, 3);
            imshow("4-GausinaBlur-to-Canny", image2);
            //-------------------------------------------------
            // Finding contours
            Point[][] contours = new Point[0][];
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(image2, out contours, out hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple, new Point());
            Point[][] contours_poly = new Point[contours.Length][];
            Rect[] boundRect = new Rect[contours.Length];
            Rect[] boundRect2 = new Rect[contours.Length];

            //-------------------------------------------------
            // Bind rectangle to every rectangle.
            for (int i = 0; i < contours.Length; i++)
            {
                contours_poly[i] = Cv2.ApproxPolyDP(contours[i], 1, true);
                boundRect[i] = Cv2.BoundingRect(contours_poly[i]);
            }
            var drawing = Mat.Zeros(image2.Size(), MatType.CV_8UC3);
            var drawingMat = drawing.ToMat();
            int refinery_count = 0;
            for (int i = 0; i < contours.Length; i++)
            {

                var ratio = (double)boundRect[i].Height / boundRect[i].Width;

                // Filtering rectangles height/width ratio, and size.
                //if ((ratio <= 2.5) && (ratio >= 0.5) && ((boundRect[i].Width * boundRect[i].Height) <= 700) && ((boundRect[i].Width * boundRect[i].Height) >= 100))
                if ((ratio <= 2.5) && (ratio >= 0.5))//&& ((boundRect[i].Width * boundRect[i].Height) <= 700)) //&& ((boundRect[i].Width * boundRect[i].Height) >= 100))
                {
                    Cv2.DrawContours(drawingMat, contours, i, new Scalar(0, 255, 255), 1, LineTypes.Link8, hierarchy, 0, new Point());
                    Cv2.Rectangle(drawingMat, boundRect[i].TopLeft, boundRect[i].BottomRight, new Scalar(255, 0, 0), 1, LineTypes.Link8, 0);

                    // Include only suitable rectangles.
                    boundRect2[refinery_count] = boundRect[i];
                    refinery_count += 1;
                }
            }

            Array.Resize(ref boundRect2, refinery_count);
            imshow("Contours&Rectangles", drawingMat);


            //-------------------------------------------------
            //  Bubble Sort accordance with X-coordinate.
            Rect temp_rect;
            for (int i = 0; i < boundRect2.Length; i++)
            {
                for (int j = 0; j < (boundRect2.Length - i - 1); j++)
                {
                    if (boundRect2[j].TopLeft.X > boundRect2[j + 1].TopLeft.X)
                    {
                        temp_rect = boundRect2[j];
                        boundRect2[j] = boundRect2[j + 1];
                        boundRect2[j + 1] = temp_rect;
                    }
                }
            }
            //-------------------------------------------------
            var friend_count = 0;
            int select = 0;
            for (int i = 0; i < boundRect2.Length; i++)
            {

                Cv2.Rectangle(image3, boundRect2[i].TopLeft, boundRect2[i].BottomRight, new Scalar(0, 255, 0), 1, LineTypes.Link8, 0);
                Cv2.PutText(image3, i.ToString(), boundRect2[i].TopLeft, HersheyFonts.HersheyPlain, 0.5, new Scalar(255, 0, 0));

                var count2 = 0;

                //  Snake moves to right, for eating his freind.
                for (int j = i + 1; j < boundRect2.Length; j++)
                {

                    double delta_x3 = Math.Abs(boundRect2[j].TopLeft.X - boundRect2[i].TopLeft.X);

                    if (delta_x3 > 150)  //  Can't eat snake friend too far ^-^.
                        break;

                    var delta_y3 = Math.Abs(boundRect2[j].TopLeft.Y - boundRect2[i].TopLeft.Y);


                    //  If delta length is 0, it causes a divide-by-zero error.
                    if (delta_x3 == 0)
                    {
                        delta_x3 = 1;
                    }

                    if (delta_y3 == 0)
                    {
                        delta_y3 = 1;
                    }

                    var gradient = delta_y3 / delta_x3;  //  Get gradient.
                                                         //cout << gradient << endl;

                    if (gradient < 0.25)
                    {  //  Can eat friends only on straight line.
                        count2 += 1;
                    }
                }

                //  Find the most full snake.
                if (count2 > friend_count)
                {
                    select = i;  //  Save most full snake number (select is the first car number)
                    friend_count = count2;  //  Renewal number of friends hunting.
                    Cv2.Rectangle(image3, boundRect2[select].TopLeft, boundRect2[select].BottomRight, new Scalar(255, 0, 255), 1, LineTypes.Link8, 0);
                    Cv2.PutText(image3, select.ToString(), boundRect2[select].TopLeft, HersheyFonts.HersheyPlain, 0.5, new Scalar(0, 0, 255));
                }
            }
            imshow("RectanglesOnPlate1", image3);

            //-------------------------------------------------
            // I know the first location of the car plate, so i can get numbers and letters on car plate
            List<Rect> carNumber = new List<Rect>(); ; // Space for real car numbers and letter
            var count = 1;

            carNumber.Add(boundRect2[select]);
            Cv2.Rectangle(image4, boundRect2[select].TopLeft, boundRect2[select].BottomRight, new Scalar(0, 255, 0), 1, LineTypes.Link8, 0);

            for (int i = 0; i < boundRect2.Length; i++)
            {
                if (boundRect2[select].TopLeft.X > boundRect2[i].TopLeft.X)   // The rest of the car plate numbers are on the right side of the first number
                    continue;

                var delta_x2 = Math.Abs(boundRect2[select].TopLeft.X - boundRect2[i].TopLeft.X);

                if (delta_x2 > 50)   // Car numbers are close to each other
                    continue;

                var delta_y2 = Math.Abs(boundRect2[select].Y - boundRect2[i].Y);

                if (delta_x2 == 0)
                {
                    delta_x2 = 1;
                }

                if (delta_y2 == 0)
                {
                    delta_y2 = 1;
                }

                var gradient = delta_y2 / delta_x2;  // Get gradient

                if (gradient < 0.25)
                {
                    select = i;
                    carNumber.Add(boundRect2[i]);
                    Cv2.Rectangle(image4, boundRect2[i].TopLeft, boundRect2[i].BottomRight, new Scalar(0, 255, 0), 1, LineTypes.Link8, 0);
                    count += 1;
                }
            }
            imshow("RectanglesOnPlate", image4);
            //-------------------------------------------------
            // Image processing is performed to increase the recognition rate of tesseract-OCR
            // The first is to rotate the tilted car plate straight
            var cropped_image = image.Clone();
            Point center1 = (carNumber[0].TopLeft + carNumber[0].BottomRight) * 0.5;  // Center of the first number
            Point center2 = (carNumber[carNumber.Count - 1].TopLeft + carNumber[carNumber.Count - 1].BottomRight) * 0.5;  // Center of the last number
            int plate_center_x = (int)((center1.X + center2.X) * 0.5);    // X-coordinate at the Center of car plate
            int plate_center_y = (int)((center1.Y + center2.Y) * 0.5);    // Y-coordinate at the Center of car plate

            // To calculate the height
            int sum_height = 0;
            for (int i = 0; i < carNumber.Count; i++)
                sum_height += carNumber[i].Height;

            var plate_width = (-center1.X + center2.X + carNumber[carNumber.Count - 1].Width) * 1.05;  // Car plate width with some paddings
            var plate_height = (int)(sum_height / carNumber.Count) * 1.2;  // Car plate height with some paddings

            var delta_x = center1.X - center2.X;
            var delta_y = center1.Y - center2.Y;
            //-------------------------------------------------
            // Roatate car plate
            double angle_degree = (Math.Atan(delta_y / delta_x)) * (double)(180 / 3.141592);

            Mat rotation_matrix = Cv2.GetRotationMatrix2D(new Point(plate_center_x, plate_center_y), angle_degree, 1.0);
            Cv2.WarpAffine(cropped_image, cropped_image, rotation_matrix, new Size(original_width, original_height));
            imshow("Rotated", cropped_image);
            //-------------------------------------------------

            Cv2.GetRectSubPix(image, new Size(plate_width, plate_height), new Point(plate_center_x, plate_center_y), cropped_image, -1);
            //imshow("Cropped", cropped_image);
            Cv2.CvtColor(cropped_image, cropped_image, ColorConversionCodes.BGR2GRAY);
            //imshow("CroppedGray", cropped_image);
            Cv2.GaussianBlur(cropped_image, cropped_image, new Size(5, 5), 0);
            //imshow("GausianBlur", cropped_image);
            Cv2.AdaptiveThreshold(cropped_image, cropped_image, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.BinaryInv, 19, 9);
            //imshow("Threshold", cropped_image);
            //threshold(cropped_image, cropped_image, 127, 255, 0);
            //imshow("Threshold", cropped_image);
            Cv2.CopyMakeBorder(cropped_image, cropped_image, 10, 10, 10, 10, BorderTypes.Constant, new Scalar(0, 0, 0)); // Padding for recognition rate
                                                                                                                         //imshow("Padded", cropped_image);

            imshow("temp", cropped_image);
            Cv2.ImWrite(Path.Combine(BaseDirectory,"carImage/temp" + currentFileNameExtension), cropped_image);  // Save the result
            printCarNumber(cropped_image);
            Cv2.WaitKey();
        }



        static void imshow(string name, Mat? img)
        {
            using (new Window(name, img))
            {
                Cv2.WaitKey();
            };
            if (!Path.Exists(Path.Combine(BaseDirectory, @"_output")))
                {
                Directory.CreateDirectory(Path.Combine(BaseDirectory, @"_output"));
            }
            img.SaveImage(Path.Combine(BaseDirectory, @"_output\" + name + currentFileNameExtension));
        }



        static string BaseDirectory => Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private static string currentFileNameExtension { get; set; }

        //*********************************************
        //** OCR
        static void printCarNumber(Mat? image)
        {
            // Open car plate image
            //image = imread("carImage/temp.JPG");
            Cv2.Resize(image, image, new Size(image.Cols * 2, image.Rows * 2), 0, 0, InterpolationFlags.Linear);
            imshow("EnlargedCarPlate", image);


            // Open input image with leptonica library
            //Pix* carNumber = pixRead("carImage/temp.jpg");

            using var engine = new Engine(@"tessdata", new List<Language> { Language.Korean }, EngineMode.TesseractOnly);
            using var img = TesseractOCR.Pix.Image.LoadFromFile(Path.Combine(BaseDirectory, "carImage/temp" + currentFileNameExtension));
            //using var page = engine.Process(img,new Rect(318, 368, 215, 47), PageSegMode.SingleBlock);
            using var page = engine.Process(img, PageSegMode.SingleLine);
            Console.WriteLine("Mean confidence: {0}", page.MeanConfidence);
            Console.WriteLine("Text: \r\n{0}", getCarNumber( page.Text));
            //Console.WriteLine("--------------------------------------\r\n");
            //Console.WriteLine(printPage(page));
        }


        private static string printPage(Page page)
        {
            StringBuilder result = new StringBuilder();
            foreach (var block in page.Layout)
            {
                result.AppendLine($"Block confidence: {block.Confidence}");
                if (block.BoundingBox != null)
                {
                    var boundingBox = block.BoundingBox.Value;
                    result.AppendLine($"Block bounding box X1 '{boundingBox.X1}', Y1 '{boundingBox.Y2}', X2 " +
                                      $"'{boundingBox.X2}', Y2 '{boundingBox.Y2}', width '{boundingBox.Width}', height '{boundingBox.Height}'");
                }
                result.AppendLine($"Block text: {block.Text}");

                foreach (var paragraph in block.Paragraphs)
                {
                    result.AppendLine($"Paragraph confidence: {paragraph.Confidence}");
                    if (paragraph.BoundingBox != null)
                    {
                        var boundingBox = paragraph.BoundingBox.Value;
                        result.AppendLine($"Paragraph bounding box X1 '{boundingBox.X1}', Y1 '{boundingBox.Y2}', X2 " +
                                          $"'{boundingBox.X2}', Y2 '{boundingBox.Y2}', width '{boundingBox.Width}', height '{boundingBox.Height}'");
                    }
                    var info = paragraph.Info;
                    result.AppendLine($"Paragraph info justification: {info.Justification}");
                    result.AppendLine($"Paragraph info is list item: {info.IsListItem}");
                    result.AppendLine($"Paragraph info is crown: {info.IsCrown}");
                    result.AppendLine($"Paragraph info first line ident: {info.FirstLineIdent}");
                    result.AppendLine($"Paragraph text: {paragraph.Text}");

                    foreach (var textLine in paragraph.TextLines)
                    {
                        if (textLine.BoundingBox != null)
                        {
                            var boundingBox = textLine.BoundingBox.Value;
                            result.AppendLine($"Text line bounding box X1 '{boundingBox.X1}', Y1 '{boundingBox.Y2}', X2 " +
                                              $"'{boundingBox.X2}', Y2 '{boundingBox.Y2}', width '{boundingBox.Width}', height '{boundingBox.Height}'");
                        }
                        result.AppendLine($"Text line confidence: {textLine.Confidence}");
                        result.AppendLine($"Text line text: {textLine.Text}");

                        foreach (var word in textLine.Words)
                        {
                            result.AppendLine($"Word confidence: {word.Confidence}");
                            if (word.BoundingBox != null)
                            {
                                var boundingBox = word.BoundingBox.Value;
                                result.AppendLine($"Word bounding box X1 '{boundingBox.X1}', Y1 '{boundingBox.Y2}', X2 " +
                                                  $"'{boundingBox.X2}', Y2 '{boundingBox.Y2}', width '{boundingBox.Width}', height '{boundingBox.Height}'");
                            }
                            result.AppendLine($"Word is from dictionary: {word.IsFromDictionary}");
                            result.AppendLine($"Word is numeric: {word.IsNumeric}");
                            result.AppendLine($"Word language: {word.Language}");
                            result.AppendLine($"Word text: {word.Text}");

                            foreach (var symbol in word.Symbols)
                            {
                                result.AppendLine($"Symbol confidence: {symbol.Confidence}");
                                if (symbol.BoundingBox != null)
                                {
                                    var boundingBox = symbol.BoundingBox.Value;
                                    result.AppendLine($"Symbol bounding box X1 '{boundingBox.X1}', Y1 '{boundingBox.Y2}', X2 " +
                                                      $"'{boundingBox.X2}', Y2 '{boundingBox.Y2}', width '{boundingBox.Width}', height '{boundingBox.Height}'");
                                }
                                result.AppendLine($"Symbol is superscript: {symbol.IsSuperscript}");
                                result.AppendLine($"Symbol is dropcap: {symbol.IsDropcap}");
                                result.AppendLine($"Symbol text: {symbol.Text}");
                            }
                        }
                    }
                }
            }

            return result.ToString();
        }


        private static string getCarNumber(string text)
        {
            int i = 0;

            //cout << test << '\n';

            // Extract "12��3456" or "123��4567"
            //Regex re=new Regex(@"\d{2,3}\W{2}\s{0,}\d{4}");
            Regex re = new Regex(@"\s{0,}\d{4}");
            MatchCollection matched= re.Matches(text);
            var Result=String.Join("", matched.Select(x => x.Value));
            return Result;

        }
    }
}
