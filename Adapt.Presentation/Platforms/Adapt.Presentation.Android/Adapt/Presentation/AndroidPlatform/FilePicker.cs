using Adapt.Presentation;
using Android.Content;
using Android.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Adapt.Presentation.AndroidPlatform
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    ///
    [Preserve(AllMembers = true)]
    public class FilePicker : IFilePicker
    {
        #region Fields
        private int _requestId;
        private TaskCompletionSource<FileData> _completionSource;
        private readonly Context _Context;
        #endregion

        #region Constructor
        public FilePicker(Context context)
        {
            _Context = context;
        }
        #endregion

        #region Private Methods
        private int GetRequestId()
        {
            var id = _requestId;

            if (_requestId == int.MaxValue)
                _requestId = 0;
            else
                _requestId++;

            return id;
        }

        private Task<FileData> PickAndOpenFile(bool isSave)
        {
            var id = GetRequestId();

            var ntcs = new TaskCompletionSource<FileData>(id);

            if (Interlocked.CompareExchange(ref _completionSource, ntcs, null) != null)
                throw new InvalidOperationException("Only one operation can be active at a time");

            try
            {
                var pickerIntent = new Intent(_Context, typeof(FilePickerActivity));
                pickerIntent.SetFlags(ActivityFlags.NewTask);

                _Context.StartActivity(pickerIntent);

                EventHandler<FilePickerEventArgs> handler = null;
                EventHandler<EventArgs> cancelledHandler = null;

                handler = (s, e) =>
                {
                    var tcs = Interlocked.Exchange(ref _completionSource, null);

                    FilePickerActivity.FilePicked -= handler;

                    Stream fileStream;
                    if (isSave)
                    {
                        fileStream = File.OpenRead(e.FilePath);
                    }
                    else
                    {
                        fileStream = File.OpenWrite(e.FilePath);
                    }

                    tcs?.SetResult(new FileData { FileName = e.FileName, FileStream = fileStream });
                };

                cancelledHandler = (s, e) =>
                {
                    var tcs = Interlocked.Exchange(ref _completionSource, null);

                    FilePickerActivity.FilePickCancelled -= cancelledHandler;

                    tcs?.SetResult(null);
                };

                FilePickerActivity.FilePickCancelled += cancelledHandler;
                FilePickerActivity.FilePicked += handler;
            }
            catch (Exception exAct)
            {
                Debug.Write(exAct);
            }

            return _completionSource.Task;
        }

        #endregion

        #region Public Methods (Implementation)
        public Task<FileData> PickAndOpenFileForReading()
        {
            return PickAndOpenFile(false);
        }

        public async Task<FileData> PickAndOpenFileForWriting(IDictionary<string, IList<string>> fileTypes, string fileName)
        {
            var directory = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, Android.OS.Environment.DirectoryDownloads);

            var filePath = Path.Combine(directory, fileName);

            var retVal = new FileData { FileName = fileName };

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            retVal.FileStream = File.Create(filePath);

            return retVal;
        }
        #endregion
    }
}