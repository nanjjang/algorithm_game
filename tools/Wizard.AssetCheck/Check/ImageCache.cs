using System.Collections.Generic;
using System.IO;
using Wizard.AssetCheck.Png;

namespace Wizard.AssetCheck.Check
{
    // 그림 파일을 한 번만 읽는다. 경로는 art/production 기준('/' 구분). 읽지 못한 까닭도 들고 있다
    public sealed class ImageCache
    {
        private readonly string _artRoot;
        private readonly Dictionary<string, PngImage?> _images = new Dictionary<string, PngImage?>(System.StringComparer.Ordinal);
        private readonly Dictionary<string, string> _errors = new Dictionary<string, string>(System.StringComparer.Ordinal);

        public ImageCache(string artRoot)
        {
            _artRoot = artRoot;
        }

        public string FullPath(string artPath) => Path.Combine(_artRoot, artPath.Replace('/', Path.DirectorySeparatorChar));

        public bool Exists(string artPath) => File.Exists(FullPath(artPath));

        // 없거나 읽지 못하면 false 와 까닭
        public bool TryGet(string artPath, out PngImage image, out string error)
        {
            if (!_images.TryGetValue(artPath, out PngImage? cached))
            {
                cached = Load(artPath);
                _images.Add(artPath, cached);
            }
            if (cached == null)
            {
                image = null!;
                error = _errors[artPath];
                return false;
            }
            image = cached;
            error = "";
            return true;
        }

        private PngImage? Load(string artPath)
        {
            string full = FullPath(artPath);
            if (!File.Exists(full))
            {
                _errors[artPath] = "file not found";
                return null;
            }
            try
            {
                return PngImage.Load(full);
            }
            catch (PngFormatException e)
            {
                _errors[artPath] = "not a readable PNG: " + e.Message;
                return null;
            }
        }
    }
}
