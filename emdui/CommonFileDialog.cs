using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace emdui
{
    internal class CommonFileDialog
    {
        private bool _saving;
        private List<string> _extensions = new List<string>();
        private string _defaultFileName;

        public static CommonFileDialog Open()
        {
            return new CommonFileDialog(false);
        }

        public static CommonFileDialog Save()
        {
            return new CommonFileDialog(true);
        }

        private CommonFileDialog(bool saving)
        {
            _saving = saving;
        }

        public CommonFileDialog WithDefaultFileName(string fileName)
        {
            _defaultFileName = fileName;
            return this;
        }

        public CommonFileDialog AddExtension(string extension)
        {
            _extensions.Add(extension);
            return this;
        }

        private string GetExtensionName(string extension)
        {
            if (extension.StartsWith("*."))
            {
                extension = extension.Substring(2);
            }
            return extension.ToUpper() + " Files";
        }

        public void Show(Action<string> callback)
        {
            var dialog = _saving ? (FileDialog)new SaveFileDialog() : new OpenFileDialog();
            if (Path.IsPathRooted(_defaultFileName))
                dialog.FileName = _defaultFileName;
            else
                dialog.FileName = Path.Combine(dialog.InitialDirectory, _defaultFileName);
            dialog.Filter = string.Join("|", _extensions
                .Select(x => $"{GetExtensionName(x)} ({x})|{x}"));

            if (_extensions.Count > 1)
            {
                var allExtensions = string.Join(";", _extensions);
                dialog.Filter = $"All Supported Files ({allExtensions})|{allExtensions}|{dialog.Filter}";
            }
            if (dialog.ShowDialog() == true)
            {
                callback(dialog.FileName);
            }
        }
    }
}
