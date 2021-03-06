using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SpriteSheetTool
{
    public static class SpriteSheet
    {
        /// <summary>
        /// Used to append on file names for the case of creating a sequence of images.
        /// </summary>
        private static string FILE_NUMBER = "[file_number]";

        public delegate void UpdateProgressBar();

        /// <summary>
        /// Returns list of all known system colors, including 'transparent'.
        /// </summary>
        public static List<string> ColorList
        {
            get
            {
                List<string> output = new List<string>();

                foreach (string color_name in Enum.GetNames(typeof(KnownColor)))
                {
                    KnownColor known_color = (KnownColor)Enum.Parse(typeof(KnownColor), color_name);

                    // Deliberately keeping KnownColor.Transparent.
                    if (known_color > KnownColor.WindowText && known_color < KnownColor.ButtonFace)
                    {
                        output.Add(color_name);
                    }
                }

                return output;
            }
        }
        /// <summary>
        /// Returns a list of supported file types.
        /// </summary>
        public static List<string> SupportedFormatsList
        {
            get
            {
                List<string> output = new List<string>();

                Type type = typeof(ImageFormat);
                MemberInfo[] methods_list = type.GetMembers(BindingFlags.Static | BindingFlags.Public | BindingFlags.GetProperty);

                foreach (MemberInfo info in methods_list)
                {
                    if (info.MemberType == MemberTypes.Property && info.Name != "MemoryBmp")
                    {
                        output.Add(info.Name);
                    }
                }

                return output;
            }
        }

        /// <summary>
        /// Creates a sprite sheet from a list of bitmap file names.
        /// </summary>
        /// <param name="file_names">list object containing filenames of images to include in spritesheet.</param>
        /// <param name="image_pattern">object containing tiling pattern information.</param>
        /// <param name="background_color">background color of spritesheet. Only important if tiling pattern doesn't 
        /// completely cover spritesheet. Ignored if parameter is set transparent.</param>
        /// <param name="mask_color">color of overlay mask to apply after spritesheet is built. No mask is set if
        /// parameter is set to transparent.</param>
        /// <param name="file_name">Name of output file. Do not include the file type or path.</param>
        /// <param name="output_directory">Path to output file directory. Will be created if it does not exist.</param>
        /// <param name="output_file_type">ImageFormat enumeration of file type to generate.</param>
        /// <returns>bool flag indicating if any errors occured while processing sprite sheet.</returns>
        public static bool CreateSpriteSheet(List<string> image_file_names, Point image_pattern, KnownColor background_color, KnownColor mask_color, string raw_file_name, string output_directory, string raw_image_format)
        {
            return CreateSpriteSheet(image_file_names, image_pattern, background_color, mask_color, raw_file_name, output_directory, raw_image_format, null);
        }

        /// <summary>
        /// Creates a sprite sheet from a list of bitmap file names.
        /// </summary>
        /// <param name="file_names">list object containing filenames of images to include in spritesheet.</param>
        /// <param name="image_pattern">object containing tiling pattern information.</param>
        /// <param name="background_color">background color of spritesheet. Only important if tiling pattern doesn't 
        /// completely cover spritesheet. Ignored if parameter is set transparent.</param>
        /// <param name="mask_color">color of overlay mask to apply after spritesheet is built. No mask is set if
        /// parameter is set to transparent.</param>
        /// <param name="file_name">Name of output file. Do not include the file type or path.</param>
        /// <param name="output_directory">Path to output file directory. Will be created if it does not exist.</param>
        /// <param name="output_file_type">ImageFormat enumeration of file type to generate.</param>
        /// <param name="update_progress_counter">A delegate callback to some external counter.</param>
        /// <returns>bool flag indicating if any errors occured while processing sprite sheet.</returns>
        public static bool CreateSpriteSheet(List<string> image_file_names, Point image_pattern, KnownColor background_color, KnownColor mask_color, string raw_file_name, string output_directory, string raw_image_format, UpdateProgressBar update_progress_counter)
        {
            ImageFormat parsed_image_format = ParseOutputType(raw_image_format);

            try
            {
                // Load up images
                List<Bitmap> bitmaps = new List<Bitmap>();

                foreach (string image_file_name in image_file_names)
                {
                    using (FileStream file_stream = new FileStream(image_file_name, FileMode.Open))
                        bitmaps.Add(new Bitmap(file_stream));
                }

                Point image_size = GetLargestImageDimensions(bitmaps);
                Bitmap sprite_sheet = new Bitmap(image_size.X * image_pattern.X, image_size.Y * image_pattern.Y);

                // WAIT A FUCKING SECOND, does this even do anything?
                //Set Background color
                if (background_color != KnownColor.Transparent)
                {
                    Graphics graphics_obj = Graphics.FromImage(sprite_sheet);
                    graphics_obj.Clear(Color.FromKnownColor(background_color));
                    graphics_obj.Dispose();
                }

                // Copy images
                int x, y, z;
                Point current_image = new Point(0, 0);

                for (z = 0; z < bitmaps.Count; z++)
                {
                    for (x = 0; x < image_size.X; x++)
                    {
                        for (y = 0; y < image_size.Y; y++)
                        {
                            Color color = bitmaps[z].GetPixel(x, y);
                            sprite_sheet.SetPixel(image_size.X * current_image.X + x, image_size.Y * current_image.Y + y, color);
                        }
                    }

                    // Update where we are on spritesheet
                    current_image.X += 1;

                    if (current_image.X == image_pattern.X)
                    {
                        current_image.X = 0;
                        current_image.Y += 1;
                    }

                    if (current_image.Y == image_pattern.Y)
                        current_image.Y = 0;

                    if (update_progress_counter != null)
                        update_progress_counter.Invoke();
                }

                // set color mask
                if (mask_color != KnownColor.Transparent)
                    sprite_sheet.MakeTransparent(Color.FromKnownColor(mask_color));

                // save file
                if (!Directory.Exists(output_directory))
                    Directory.CreateDirectory(output_directory);

                string file_path;

                if (output_directory.LastIndexOf('\\') == output_directory.Length - 1)
                    file_path = output_directory + raw_file_name;
                else
                    file_path = output_directory + "\\" + raw_file_name;

                int file_number = 0;
                string file_name = BuildFileName(raw_file_name, image_size, image_file_names.Count, output_directory, parsed_image_format);
                string output_file_name = file_name.Replace(FILE_NUMBER, string.Empty);

                // first time we try without the file number
                while (File.Exists(output_file_name))
                {
                    ++file_number;
                    output_file_name = file_name.Replace(FILE_NUMBER, "(" + file_number.ToString() + ")");
                }

                using (FileStream file_stream = new FileStream(output_directory + "\\" + output_file_name, FileMode.Create))
                    sprite_sheet.Save(file_stream, parsed_image_format);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Seperates a SpriteSheet into individual images.
        /// </summary>
        /// <param name="spritesheet_filename">Path and file name of existing SpriteSheet to load</param>
        /// <param name="image_pattern">object containing tiling pattern information.</param>
        /// <param name="file_name">Name of output file. Do not include the file type or path.</param>
        /// <param name="output_directory">Path to output file directory. Will be created if it does not exist.</param>
        /// <param name="output_file_type">ImageFormat enumeration of file type to generate.</param>
        /// <returns>bool flag indicating if any errors occured while processing sprite sheet.</returns>
        public static bool SeperateSpriteSheet(string spritesheet_filename, Point image_pattern, string output_filename, string output_directory, string output_file_type)
        {
            return SeperateSpriteSheet(spritesheet_filename, image_pattern, output_filename, output_directory, output_file_type, null);
        }

        /// <summary>
        /// Seperates a SpriteSheet into individual images.
        /// </summary>
        /// <param name="spritesheet_filename">Path and file name of existing SpriteSheet to load</param>
        /// <param name="image_pattern">object containing tiling pattern information.</param>
        /// <param name="file_name">Name of output file. Do not include the file type or path.</param>
        /// <param name="output_directory">Path to output file directory. Will be created if it does not exist.</param>
        /// <param name="output_file_type">ImageFormat enumeration of file type to generate.</param>
        /// <param name="update_progress_counter">delegate callback to some external counter</param>
        /// <returns>bool flag indicating if any errors occured while processing sprite sheet.</returns>
        public static bool SeperateSpriteSheet(string spritesheet_filename, Point image_pattern, string output_filename, string output_directory, string output_file_type, UpdateProgressBar update_progress_counter)
        {
            ImageFormat image_format = ParseOutputType(output_file_type);

            try
            {
                if (!Directory.Exists(output_directory))
                    Directory.CreateDirectory(output_directory);

                // Load spritsheet - why so filestream?
                //file_stream = new FileStream(spritesheet_filename, FileMode.Open);
                //Bitmap sprite_sheet = new Bitmap(file_stream);
                //file_stream.Close();

                Bitmap sprite_sheet = new Bitmap(spritesheet_filename);

                //List<Bitmap> output_file_list = new List<Bitmap>(); 
                Bitmap output_file;

                Point image_size = new Point(sprite_sheet.Width / image_pattern.X, sprite_sheet.Height / image_pattern.Y);
                Point current_image = new Point(0, 0);

                // Set up output buffer
                int count = (image_pattern.X * image_pattern.Y);

                for (int i = 0; i < count; i++)
                {
                    output_file = new Bitmap(image_size.X, image_size.Y);

                    // Copy images
                    int x, y;

                    for (x = 0; x < image_size.X; x++)
                    {
                        for (y = 0; y < image_size.Y; y++)
                        {
                            Color color = sprite_sheet.GetPixel(image_size.X * current_image.X + x, image_size.Y * current_image.Y + y);
                            output_file.SetPixel(x, y, color);
                        }
                    }

                    // Update where we are on spritesheet
                    current_image.X += 1;

                    if (current_image.X == image_pattern.X)
                    {
                        current_image.X = 0;
                        current_image.Y += 1;
                    }

                    if (current_image.Y == image_pattern.Y)
                        current_image.Y = 0;

                    string file_counter = (i + 1).ToString();

                    while (file_counter.Length < 4)
                        file_counter = "0" + file_counter;

                    string filename = output_filename + "(" + file_counter + ")." + image_format.ToString().ToLower();

                    using (FileStream file_stream = new FileStream(output_directory + "\\" + filename, FileMode.Create))
                        output_file.Save(file_stream, image_format);

                    if (update_progress_counter != null)
                        update_progress_counter.Invoke();
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public static Point CalculateSpriteSheetOrganization(int image_count, Point image_dimensions)
        {
            // crazy stuff here. we want a collection of unique factor pairs,
            // ordered by the smallest difference between the height and width
            // so that we dont get strange long or tall sheets that don't fit in video 
            // memory. a 3x4 sheet is often better than a 11x1 sheet, despite wasted 
            // pixles. also, we multiply by the actual image dimensions so that non-square
            // images don't get into trouble. i.e. 50 images 10x500 arranged in a 5x10
            // spritesheet. 50x5000 is far less manageable than 500x500.

            // we also need to consider that the most optimal grid arrangement in terms of
            // building a square tiling might waste a fair ammount of spritesheet space.

            // upper_factor_limit is the additional 'headroom' in the factor search space we are adding
            // to allow for more arrangements that could be slightly larger than the image count.
            // Again think of 11 images organized in 3x4 grid over 11x1.

            int upper_factor_limit = (int)(image_count * 1.2);
            List<Tuple<int, int>> factor_list = new List<Tuple<int, int>>();

            // list is of (factor, factor celing), since we can have multiple celing values
            for (int i = image_count; i <= upper_factor_limit; i++)
                factor_list.AddRange(GetFactors(i));

            Dictionary<Point, int> set_list = new Dictionary<Point, int>();

            foreach (var factor1 in factor_list)
            {
                Point pair = new Point(factor1.Item1, factor1.Item2 / factor1.Item1);

                if (!set_list.ContainsKey(pair))
                    set_list.Add(pair, (Math.Abs((pair.X * image_dimensions.X) - (pair.Y * image_dimensions.Y))));
            }

            // this should be abs(ratio of height : ratio of width - 1), sorted by lowest first

            var sorted_dictionary = (from entry in set_list
                                     orderby entry.Value ascending
                                     select entry);

            return sorted_dictionary.First().Key;
        }

        public static Point GetLargestImageDimensions(List<Bitmap> bitmap_list)
        {
            Point largest_bitmap = new Point(0, 0);

            if (bitmap_list == null)
                return largest_bitmap;

            foreach (var bitmap in bitmap_list)
            {
                if (bitmap.Width > largest_bitmap.X)
                    largest_bitmap.X = bitmap.Width;

                if (bitmap.Height > largest_bitmap.Y)
                    largest_bitmap.Y = bitmap.Height;
            }

            return largest_bitmap;
        }

        public static Point GetSmallestImage(List<Bitmap> bitmap_list)
        {
            if (bitmap_list == null || bitmap_list.Count == 0)
                return new Point(0, 0);

            Point smallest_bitmap = new Point(bitmap_list[0].Width, bitmap_list[0].Height);

            foreach (var bitmap in bitmap_list)
            {
                if (bitmap.Width > smallest_bitmap.X)
                    smallest_bitmap.X = bitmap.Width;

                if (bitmap.Height > smallest_bitmap.Y)
                    smallest_bitmap.Y = bitmap.Height;
            }

            return smallest_bitmap;
        }

        public static bool AreAllImagesSameSize(List<Bitmap> bitmap_list)
        {
            if (bitmap_list == null || bitmap_list.Count == 0)
                return true;

            Point first_bitmap = new Point(bitmap_list[0].Width, bitmap_list[0].Height);

            foreach (Bitmap bitmap in bitmap_list)
            {
                if (bitmap.Width != first_bitmap.X || bitmap.Height != first_bitmap.Y)
                    return false;
            }

            return true;
        }

        public static Bitmap GetBitmapSubsection(Bitmap bitmap, Point upper_left, Point lower_right)
        {
            if (bitmap == null || upper_left == null || lower_right == null)
                return null;

            if (upper_left.X < 0) upper_left.X = 0;
            if (upper_left.Y < 0) upper_left.Y = 0;

            if (lower_right.X < 0) lower_right.X = 0;
            if (lower_right.Y < 0) lower_right.Y = 0;

            if (upper_left.X > bitmap.Width) upper_left.X = bitmap.Width;
            if (upper_left.Y > bitmap.Height) upper_left.Y = bitmap.Height;

            if (lower_right.X > bitmap.Width) lower_right.X = bitmap.Width;
            if (lower_right.Y > bitmap.Height) lower_right.Y = bitmap.Height;

            if (upper_left.X > lower_right.X || upper_left.Y > lower_right.Y)
                return null;

            Bitmap output = new Bitmap(lower_right.X - upper_left.X, lower_right.Y - upper_left.Y);

            int height = (lower_right.X - upper_left.X);
            int width = (lower_right.X - upper_left.X);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color color = bitmap.GetPixel(x + upper_left.X, y + upper_left.Y);
                    output.SetPixel(x, y, color);
                }
            }

            return output;
        }

        public static string GetUniqueImageName(string file_name, ImageFormat image_type)
        {
            return file_name + DateTime.Now.ToFileTime().ToString() + image_type.ToString();
        }

        private static ImageFormat ParseOutputType(string input)
        {
            switch (input)
            {
                case "png": return ImageFormat.Bmp;
                //case "Emf":     return ImageFormat.Emf;
                //case "Exif":    return ImageFormat.Exif;
                case "Gif": return ImageFormat.Gif;
                //case "Icon":    return ImageFormat.Icon; 
                case "Jpeg": return ImageFormat.Jpeg;
                case "Png": return ImageFormat.Png;
                case "Tiff": return ImageFormat.Tiff;
                case "Wmf": return ImageFormat.Wmf;

                default:
                    return ImageFormat.Bmp;
            }
        }

        private static string BuildFileName(string file_name, Point image_dimensions, int image_count, string output_path, ImageFormat image_format)
        {
            return string.Format("{0}[file_number] ({1}x{2}x{3}).{4}", file_name, image_dimensions.X, image_dimensions.Y, image_count, image_format.ToString().ToLower());

            //StringBuilder sb = new StringBuilder();

            //sb.Append(file_name);
            //sb.Append(FILE_NUMBER);
            //sb.Append(" ");
            //sb.Append("(");
            //sb.Append(image_dimensions.X.ToString());
            //sb.Append("x");
            //sb.Append(image_dimensions.Y.ToString());
            //sb.Append("x");
            //sb.Append(image_count.ToString());
            //sb.Append(")");
            //sb.Append(".");
            //sb.Append(image_format.ToString().ToLower());

            //return sb.ToString();
        }

        /// <summary>
        /// Takes value X, and finds all factors 
        /// </summary>
        private static IEnumerable<Tuple<int, int>> GetFactors(int x)
        {
            int max = (int)Math.Ceiling(Math.Sqrt(x));

            for (int factor = 1; factor <= max; factor++)
            {
                if (x % factor == 0)
                {
                    yield return new Tuple<int, int>(factor, x);

                    if (factor != max)
                        yield return new Tuple<int, int>((x / factor), x);
                }
            }
        }
    }
}
