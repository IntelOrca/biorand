using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using IntelOrca.Biohazard;

namespace emdui
{
    public class Project
    {
        private readonly List<ProjectFile> _projectFiles = new List<ProjectFile>();

        public string MainPath { get; set; }

        public ModelFile MainModel { get; set; }
        public PlwFile[] Weapons { get; set; }

        public BioVersion Version => MainModel.Version;
        public IReadOnlyList<ProjectFile> Files => _projectFiles;

        public static Project FromFile(string path)
        {
            return new Project(path);
        }

        private Project(string path)
        {
            MainPath = path;

            if (path.EndsWith(".pld", System.StringComparison.OrdinalIgnoreCase))
            {
                var pldFile = ModelFile.FromFile(path) as PldFile;
                if (pldFile == null)
                    throw new Exception("Not a PLD formatted file.");

                MainModel = pldFile;
                var fileName = Path.GetFileName(path);
                _projectFiles.Add(new ProjectFile(ProjectFileKind.Pld, fileName, pldFile));

                LoadWeapons(path);
            }
            else if (path.EndsWith(".emd", System.StringComparison.OrdinalIgnoreCase))
            {
                var emdFile = ModelFile.FromFile(path) as EmdFile;
                if (emdFile == null)
                    throw new Exception("Not an EMD formatted file.");

                MainModel = emdFile;
                var fileName = Path.GetFileName(path);
                _projectFiles.Add(new ProjectFile(ProjectFileKind.Emd, fileName, emdFile));

                LoadTexture(path);
            }
        }

        private void LoadTexture(string emdPath)
        {
            var timPath = Path.ChangeExtension(emdPath, ".tim");
            if (File.Exists(timPath))
            {
                var timFile = new TimFile(timPath);
                _projectFiles.Add(new ProjectFile(ProjectFileKind.Tim, timPath, timFile));
            }
        }

        private void LoadWeapons(string pldPath)
        {
            var directory = Path.GetDirectoryName(pldPath);
            var files = Directory.GetFiles(directory);
            foreach (var plwPath in files)
            {
                var plwFileName = Path.GetFileName(plwPath);
                if (Regex.IsMatch(plwFileName, "PL[0-9A-F][0-9A-F]W[0-9A-F][0-9A-F].PLW", RegexOptions.IgnoreCase))
                {
                    LoadWeapon(plwPath);
                }
            }
        }

        private void LoadWeapon(string path)
        {
            var plwFile = ModelFile.FromFile(path) as PlwFile;
            if (plwFile is null)
                return;

            var plwFileName = Path.GetFileName(path);
            _projectFiles.Add(new ProjectFile(ProjectFileKind.Plw, plwFileName, plwFile));

            /*
            _edd = _plwFile.GetEdd(0);
            _emr = _emr.WithKeyframes(_plwFile.GetEmr(0));

            var plwbuilder = _plwFile.Md1.ToBuilder();

            var md1 = _modelFile.Md1;
            var builder = md1.ToBuilder();

            builder.Parts[11] = plwbuilder.Parts[0];

            _modelFile.Md1 = builder.ToMd1();

            var tim = _timFile;
            var plwtim = _plwFile.Tim;
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 56; x++)
                {
                    var p = plwtim.GetPixel(x, y);
                    tim.SetPixel(200 + x, 224 + y, 1, p);
                }
            }
            RefreshTimImage();
            */
        }
    }

    public class ProjectFile
    {
        public ProjectFileKind Kind { get; set; }
        public string Filename { get; set; }
        public object Content { get; set; }

        public ProjectFile(ProjectFileKind kind, string filename, object content)
        {
            Kind = kind;
            Filename = filename;
            Content = content;
        }

        public override string ToString() => Filename;
    }

    public enum ProjectFileKind
    {
        Emd,
        Pld,
        Tim,
        Plw,
    }
}
