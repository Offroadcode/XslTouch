using System;
using System.IO;
using System.Web;
using System.Xml;
using umbraco;
using umbraco.BusinessLogic;

namespace Offroadcode.Umbraco.XSLTouch
{
    public class XSLTouch : ApplicationBase
    {
        private static readonly object LockObject = new object();
        private static string _targetDirectory;
        private static string _fileFilter;
        private static FileSystemWatcher _xsltWatcher;
        protected static FileSystemWatcher DtdWatcher;

        public XSLTouch()
        {
            Log.Add(LogTypes.Debug, -1, "Loaded XSLTouch by Offroadcode.com");
            _targetDirectory = HttpContext.Current.Server.MapPath(GlobalSettings.Path + "/../xslt/");
            _fileFilter = "*.xsl*";
            Log.Add(LogTypes.Debug, -1, "XSLTouch - Watching Directory " + _targetDirectory);
            _xsltWatcher = new FileSystemWatcher(_targetDirectory, _fileFilter);
            _xsltWatcher.IncludeSubdirectories = true;
            _xsltWatcher.NotifyFilter = NotifyFilters.LastWrite;
            _xsltWatcher.Changed += new FileSystemEventHandler(OnXSLTChanged);
            _xsltWatcher.EnableRaisingEvents = true;
        }

        private static void OnXSLTChanged(object source, FileSystemEventArgs e)
        {
            lock (LockObject)
            {
                if (e.FullPath.EndsWith("_temp.xslt"))
                    return;
                _xsltWatcher.EnableRaisingEvents = false;
                Log.Add( LogTypes.Debug, -1, string.Concat( "XSLTouch - XSLT file {0} {1}", e.FullPath, e.ChangeType ) );
                TouchFiles(e.FullPath);
                _xsltWatcher.EnableRaisingEvents = true;
            }
        }

        private static void TouchFiles(string sourceFilename)
        {
            var oldValue = _targetDirectory.EndsWith("\\") ? _targetDirectory : _targetDirectory + "\\";
            var absolutePath = sourceFilename.Replace(oldValue, "").Replace("\\", "/");
            var files = Directory.GetFiles(_targetDirectory, _fileFilter);
            var tempPath = "";
            try
            {
                foreach (string path in files)
                {
                    tempPath = path;
                    if (!path.EndsWith("_temp.xslt") && path != sourceFilename)
                    {
                        string stringToTrim = File.ReadAllText(path);
                        XmlDocument xmlDocument = new XmlDocument();
                        xmlDocument.LoadXml(TrimWithUnicodeWhitespace(stringToTrim));
                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDocument.NameTable);
                        nsmgr.AddNamespace("xsl", "http://www.w3.org/1999/XSL/Transform");
                        string xpath = "/xsl:stylesheet/xsl:include[translate( @href , 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz' ) = '" + absolutePath.ToLower() + "']";
                        if (xmlDocument.SelectSingleNode(xpath, nsmgr) != null)
                        {
                            File.SetLastWriteTime(path, DateTime.Now);
                            Log.Add(LogTypes.Debug, -1, "XSLTouch - touched " + path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add(LogTypes.Error, -1, "XSLTouch encountered an error while touching file '" + tempPath + "': " + ex.Message);
            }
        }

        private static string TrimWithUnicodeWhitespace(string stringToTrim)
        {
            return stringToTrim.Trim().Trim('\xFEFF', '\x200B');
        }
    }
}
