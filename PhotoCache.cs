
namespace PhotoCache
{
    public class PhotoCache
    {
        /// <summary>
        /// 使用次数
        /// </summary>
        private int useNumber = 1;
        public int UseNumber { get => useNumber; set => useNumber = value; }

        /// <summary>
        /// 缓存对象
        /// </summary>
        private Bitmap bit;
        public Bitmap Bit { get { ++UseNumber; return bit; } set { bit = value; Time = DateTime.Now; } }
        /// <summary>
        /// 存入时间
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
    /// 图片缓存工具
    /// </summary>
    public class PhotoCacheUtil
    {
        /// <summary>
        /// 内存
        /// </summary>
        private static volatile Dictionary<string, PhotoCache> caches = new Dictionary<string, PhotoCache>(PhotoChoose.Properties.Settings.Default.cacheNum);
        /// <summary>
        /// 本地缓存路径
        /// </summary>
        private static string tmpPath = "tmp\\";
        /// <summary>
        /// 创建一个格式化程序的实例
        /// </summary>
        private static System.Runtime.Serialization.IFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        /// <summary>
        /// 线程锁
        /// </summary>
        private static Object locker = new Object();
        /// <summary>
        /// 查看一个关键字是否存在于缓存中
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
        /// 保存缓存
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
                             * 缓存优先级调度，未满足优先级的数据将从内存移出到硬盘
                             **/
                            Stream stream_item = new FileStream(tmpPath + item + ".jpg", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                            //formatter.Serialize(stream_item, cache.Bit);//序列化可极大的减少硬盘的负担，其代价是占用极多的CPU处理时间，自己悠着点！反正我是不敢用
                            byte[] t_item_byte = null;
                            try
                            {
                                t_item_byte = BitmapToBytes(new Bitmap(caches[item].Bit));
                            }
                            catch (System.InvalidOperationException)
                            {
                                //当多个线程同时引用caches[item].Bit时。。这个异常触发的时候代表着这个对象已经或正在被处理
                                continue;
                            }
                            stream_item.Write(t_item_byte, 0, t_item_byte.Length);
                            stream_item.Close();
                            lock (locker)
                            {
                                /**====================================!read me!================================================
                                 * =========此锁的存在会降低一定的缓存器性能，如果你恰巧看见并且足够牛逼，那么请优化它吧。======
                                 * 作为缓冲器的作者，我想在你优化它之前提醒你以下几点。
                                 * ①虽然此锁作为影响缓存器性能的主要因素，但它保证了流程的原子性，从而保证了数据的一致性。如果想解开这个禁锢，那么你得想好如何解决原子性和一致性的问题
                                 * ②我曾想过用数据校验来保证数据存储的一致性，从而达到不使用锁的目的。但不管如何设计算法来进行校验数据，这无疑是代表着更多的cpu开支。
                                 * ③最典型的问题是：【线程A】带着【key】来到缓存区刚找到【key1】，发现【key1】优先级不足想将它移出到硬盘，同时【线程B】也发现了【key1】并将它移出到硬盘，
                                 *  【线程A】发现本来应该由自己处理的【key1】不见了，然后一脸懵逼
                                 * ④你可以另启用一个线程，利用排序算法（冒泡、折中、拓扑..什么都行只要你牛逼）来处理无效缓存的问题（部分缓存了优先级不达标的数据）,由此来提高缓存的命中率。
                                 *   这个办法将在硬件设施达标的时候有奇效，不要问mei我为什么不做qian。
                                 * ⑤写好注释
                                 * ⑥如有指教GitHub上yujieluo联系
                                 * =====================================!read me!================================================
                                 */
                                caches.Remove(item);
                                if (!caches.ContainsKey(key))
                                {
                                    //防止添加相同的key，多线程环境下，这个流程是不可控的
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
        /// 获得缓存中的图片，按优先级，内存-》硬盘
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
        /// Bitmap转bytes
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
        /// bytes转Bitmap
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