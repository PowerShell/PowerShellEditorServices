using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Extensions;

namespace Microsoft.PowerShell.EditorServices
{
    internal class FullScriptPosition : IScriptPosition
    {
        #region Fields
        private readonly FileContext fileContext;

        private readonly BufferPosition bufferPosition;

        #endregion

        #region Properties
        public string File
        {
            get { return fileContext.Path; }
        }
        public int LineNumber
        {
            get { return bufferPosition.Line; }
        }
        public int ColumnNumber
        {
            get { return bufferPosition.Column; }
        }
        public string Line
        {
            get { return fileContext.scriptFile.GetLine(LineNumber); }
        }
        public int Offset { get; }

        #endregion

        #region Constructors

        internal FullScriptPosition(FileContext context, BufferPosition position, int offset)
        {
            fileContext = context;
            bufferPosition = position;
            Offset = offset;
        }

        #endregion


        #region Public Methods

        public string GetFullScript()
        {
            return fileContext.GetText();
        }

        #endregion
    }
}