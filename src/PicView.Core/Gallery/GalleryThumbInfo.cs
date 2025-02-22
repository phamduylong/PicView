﻿using System.Globalization;
using PicView.Core.Extensions;
using PicView.Core.Localization;

namespace PicView.Core.Gallery;

public static class GalleryThumbInfo
{
    /// <summary>
    /// Represents the data for a gallery thumbnail.
    /// </summary>
    public readonly struct GalleryThumbHolder
    {
        public string FileLocation { get; }
        public string FileName { get; }
        public string FileSize { get; }
        public string FileDate { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GalleryThumbHolder"/> struct.
        /// </summary>
        /// <param name="fileLocation">The file location of the thumbnail.</param>
        /// <param name="fileName">The file name of the thumbnail.</param>
        /// <param name="fileSize">The file size of the thumbnail.</param>
        /// <param name="fileDate">The file date of the thumbnail.</param>
        private GalleryThumbHolder(string fileLocation, string fileName, string fileSize, string fileDate)
        {
            FileLocation = fileLocation;
            FileName = fileName;
            FileSize = fileSize;
            FileDate = fileDate;
        }

        /// <summary>
        /// Gets thumbnail data for the specified index.
        /// </summary>
        /// <param name="fileInfo">The file information of the thumbnail.</param>
        /// <returns>The <see cref="GalleryThumbHolder"/> instance containing thumbnail data.</returns>
        public static GalleryThumbHolder GetThumbData(FileInfo? fileInfo)
        {
            var fileLocation = fileInfo.FullName;
            var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
            var fileSize = 
                $"{TranslationHelper.Translation.FileSize}: {fileInfo.Length.GetReadableFileSize()}";
            var fileDate = 
                $"{TranslationHelper.Translation.Modified}: {fileInfo.LastWriteTimeUtc.ToString(CultureInfo.CurrentCulture)}";

            return new GalleryThumbHolder(fileLocation, fileName, fileSize, fileDate);
        }
    }
}