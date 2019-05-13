
namespace PhotoCache
{
    public class PhotoCache
    {
        /// <summary>
        /// ʹ�ô���
        /// </summary>
        private int useNumber = 1;
        public int UseNumber { get => useNumber; set => useNumber = value; }

        /// <summary>
        /// �������
        /// </summary>
        private Bitmap bit;
        public Bitmap Bit { get { ++UseNumber; return bit; } set { bit = value; Time = DateTime.Now; } }
        /// <summary>
        /// ����ʱ��
        /// </summary>
        private DateTime time;
        public DateTime Time { get => time; set => time = value; }



        public PhotoCache()
        {

        }

        public PhotoCache(Bitmap bit)
        {
            Time = DateTime.Now;
            this.bit = bit;
        }

        ~PhotoCache()
        {

        }

    }
    /// <summary>
    /// ͼƬ���湤��
    /// </summary>
    public class PhotoCacheUtil
    {
        /// <summary>
        /// �ڴ�
        /// </summary>
        private static volatile Dictionary<string, PhotoCache> caches = new Dictionary<string, PhotoCache>(PhotoChoose.Properties.Settings.Default.cacheNum);
        /// <summary>
        /// ���ػ���·��
        /// </summary>
        private static string tmpPath = "tmp\\";
        /// <summary>
        /// ����һ����ʽ�������ʵ��
        /// </summary>
        private static System.Runtime.Serialization.IFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        /// <summary>
        /// �߳���
        /// </summary>
        private static Object locker = new Object();
        /// <summary>
        /// �鿴һ���ؼ����Ƿ�����ڻ�����
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Boolean IsExist(string key)
        {
            key = key.Split('?').Length > 1 ? key.Split('?')[1] : key;
            if (caches.ContainsKey(key))
            {
                return true;
            }

            if (!Directory.Exists("tmp"))
            {
                Directory.CreateDirectory("tmp");
            }
            string path = tmpPath + key + ".jpg";
            if (File.Exists(@path))
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// ���滺��
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cache"></param>
        public static void Save(string key, PhotoCache cache)
        {
            key = key.Split('?').Length > 1 ? key.Split('?')[1] : key;
            if (caches.ContainsKey(key))
            {
                caches[key].Time = DateTime.Now;
                return;
            }
            System.Threading.Thread cacheThread = null;
            cacheThread = new System.Threading.Thread(() =>
            {
                if (caches.Count <= 2 * PhotoChoose.Properties.Settings.Default.cacheNum && caches.Count <= 100)
                {

                    caches.Add(key, cache);
                }
                else
                {
                    DateTime t_time = DateTime.Now;
                    double temp_time = t_time.ToFileTimeUtc();
                    foreach (string item in caches.Keys)
                    {

                        if ((caches[item].UseNumber * 100000000) - (temp_time - caches[item].Time.ToFileTimeUtc()) <
                        (cache.UseNumber * 100000000) - (temp_time - cache.Time.ToFileTimeUtc()))
                        {
                            /**
                             * �������ȼ����ȣ�δ�������ȼ������ݽ����ڴ��Ƴ���Ӳ��
                             **/
                            Stream stream_item = new FileStream(tmpPath + item + ".jpg", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                            //formatter.Serialize(stream_item, cache.Bit);//���л��ɼ���ļ���Ӳ�̵ĸ������������ռ�ü����CPU����ʱ�䣬�Լ����ŵ㣡�������ǲ�����
                            byte[] t_item_byte = null;
                            try
                            {
                                t_item_byte = BitmapToBytes(new Bitmap(caches[item].Bit));
                            }
                            catch (System.InvalidOperationException)
                            {
                                //������߳�ͬʱ����caches[item].Bitʱ��������쳣������ʱ���������������Ѿ������ڱ�����
                                continue;
                            }
                            stream_item.Write(t_item_byte, 0, t_item_byte.Length);
                            stream_item.Close();
                            lock (locker)
                            {
                                /**====================================!read me!================================================
                                 * =========�����Ĵ��ڻή��һ���Ļ��������ܣ������ǡ�ɿ��������㹻ţ�ƣ���ô���Ż����ɡ�======
                                 * ��Ϊ�����������ߣ����������Ż���֮ǰ���������¼��㡣
                                 * ����Ȼ������ΪӰ�컺�������ܵ���Ҫ���أ�������֤�����̵�ԭ���ԣ��Ӷ���֤�����ݵ�һ���ԡ������⿪�����������ô��������ν��ԭ���Ժ�һ���Ե�����
                                 * ���������������У������֤���ݴ洢��һ���ԣ��Ӷ��ﵽ��ʹ������Ŀ�ġ��������������㷨������У�����ݣ��������Ǵ����Ÿ����cpu��֧��
                                 * ������͵������ǣ����߳�A�����š�key���������������ҵ���key1�������֡�key1�����ȼ������뽫���Ƴ���Ӳ�̣�ͬʱ���߳�B��Ҳ�����ˡ�key1���������Ƴ���Ӳ�̣�
                                 *  ���߳�A�����ֱ���Ӧ�����Լ�����ġ�key1�������ˣ�Ȼ��һ���±�
                                 * �������������һ���̣߳����������㷨��ð�ݡ����С�����..ʲô����ֻҪ��ţ�ƣ���������Ч��������⣨���ֻ��������ȼ����������ݣ�,�ɴ�����߻���������ʡ�
                                 *   ����취����Ӳ����ʩ����ʱ������Ч����Ҫ��mei��Ϊʲô����qian��
                                 * ��д��ע��
                                 * ������ָ��GitHub��yujieluo��ϵ
                                 * =====================================!read me!================================================
                                 */
                                caches.Remove(item);
                                if (!caches.ContainsKey(key))
                                {
                                    //��ֹ�����ͬ��key�����̻߳����£���������ǲ��ɿص�
                                    caches.Add(key, cache);
                                }
                            }

                            goto CacheFinish;
                        }
                    }
                    Stream stream_key = new FileStream(tmpPath + key + ".jpg", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    byte[] t_key_byte = BitmapToBytes(cache.Bit);
                    stream_key.Write(t_key_byte, 0, t_key_byte.Length);
                    stream_key.Close();
                    //cache.Bit.Save(tmpPath + key + ".jpg");
                }
            CacheFinish: cacheThread.Abort();
            })
            {
                Name = "cacheThread",
                IsBackground = true
            };
            cacheThread.Start();
        }
        /// <summary>
        /// ��û����е�ͼƬ�������ȼ����ڴ�-��Ӳ��
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Bitmap Get(string key)
        {
            key = key.Split('?').Length > 1 ? key.Split('?')[1] : key;
            if (caches.ContainsKey(key))
            {
                return caches[key].Bit;
            }


            if (!Directory.Exists("tmp"))
            {
                Directory.CreateDirectory("tmp");
            }
            string path = tmpPath + key + ".txt";
            if (File.Exists(path))
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] t_byte = new byte[stream.Length];
                stream.Read(t_byte, 0, Convert.ToInt32(stream.Length));
                //Bitmap bitmap = (Bitmap)formatter.Deserialize(destream);
                stream.Close();
                Bitmap bitmap = BytesToBitmap(t_byte);
                Save(key, new PhotoCache(bitmap));
                return bitmap;
            }
            return null;
        }

        /// <summary>
        /// Bitmapתbytes
        /// </summary>
        /// <param name="Bitmap"></param>
        /// <returns></returns>
        public static byte[] BitmapToBytes(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                byte[] data = new byte[stream.Length];
                stream.Seek(0, SeekOrigin.Begin);
                stream.Read(data, 0, Convert.ToInt32(stream.Length));
                return data;
            }
        }
        /// <summary>
        /// bytesתBitmap
        /// </summary>
        /// <param name="Bytes"></param>
        /// <returns></returns>
        public static Bitmap BytesToBitmap(byte[] Bytes)
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream(Bytes);
                return new Bitmap((Image)new Bitmap(stream));
            }
            catch (ArgumentNullException ex)
            {
                throw ex;
            }
            catch (ArgumentException ex)
            {
                throw ex;
            }
            finally
            {
                stream.Close();
            }
        }

    }
}