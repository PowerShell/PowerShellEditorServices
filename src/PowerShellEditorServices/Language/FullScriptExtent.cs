using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Extensions;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices
{
    /// <summary>
    /// Provides an IScriptExtent implementation that is aware of editor context
    /// and can adjust to changes.
    /// </summary>
   public class FullScriptExtent : IScriptExtent
    {
        #region Properties

        /// <summary>
        /// Gets the buffer range of the extent.
        /// </summary>
        public BufferRange BufferRange { get; private set; }

        /// <summary>
        /// Gets the FileContext that this extent refers to.
        /// </summary>
        public FileContext FileContext { get; }

        /// <summary>
        /// Gets the file path of the script file in which this extent is contained.
        /// </summary>
        public string File
        {
            get { return FileContext.Path; }
        }

        /// <summary>
        /// Gets the starting script position of the extent.
        /// </summary>
        public IScriptPosition StartScriptPosition
        {
            get { return new FullScriptPosition(FileContext, BufferRange.Start, StartOffset); }
        }

        /// <summary>
        /// Gets the ending script position of the extent.
        /// </summary>
        public IScriptPosition EndScriptPosition
        {
            get { return new FullScriptPosition(FileContext, BufferRange.End, EndOffset); }
        }

        /// <summary>
        /// Gets the starting line number of the extent.
        /// </summary>
        public int StartLineNumber
        {
            get { return BufferRange.Start.Line; }
        }


        /// <summary>
        /// Gets the starting column number of the extent.
        /// </summary>
        public int StartColumnNumber
        {
            get { return BufferRange.Start.Column; }
        }

        /// <summary>
        /// Gets the ending line number of the extent.
        /// </summary>
        public int EndLineNumber
        {
            get { return BufferRange.End.Line; }
        }

        /// <summary>
        /// Gets the ending column number of the extent.
        /// </summary>
        public int EndColumnNumber
        {
            get { return BufferRange.End.Column; }
        }

        /// <summary>
        /// Gets the text that is contained within the extent.
        /// </summary>
        public string Text
        {
            get
            {
                // StartOffset can be > the length for the EOF token.
                if (StartOffset > FileContext.scriptFile.Contents.Length)
                {
                    return "";
                }

                return FileContext.GetText(BufferRange);
            }
        }

        /// <summary>
        /// Gets the starting file offset of the extent.
        /// </summary>
        public int StartOffset { get; private set; }

        /// <summary>
        /// Gets the ending file offset of the extent.
        /// </summary>
        public int EndOffset { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of the FullScriptExtent class.
        /// </summary>
        /// <param name="fileContext">The FileContext this extent refers to.</param>
        /// <param name="bufferRange">The buffer range this extent is located at.</param>
        public FullScriptExtent(FileContext fileContext, BufferRange bufferRange)
        {
            Validate.IsNotNull(nameof(fileContext), fileContext);
            Validate.IsNotNull(nameof(bufferRange), bufferRange);

            BufferRange = bufferRange;
            FileContext = fileContext;

            StartOffset = fileContext.scriptFile.GetOffsetAtPosition(
                bufferRange.Start.Line,
                bufferRange.Start.Column);

            EndOffset = fileContext.scriptFile.GetOffsetAtPosition(
                bufferRange.End.Line,
                bufferRange.End.Column);
        }

        /// <summary>
        /// Creates an new instance of the FullScriptExtent class.
        /// </summary>
        /// <param name="fileContext">The FileContext this extent refers to.</param>
        /// <param name="startOffset">The zero based offset this extent starts at.</param>
        /// <param name="endOffset">The zero based offset this extent ends at.</param>
        public FullScriptExtent(FileContext fileContext, int startOffset, int endOffset)
        {
            Validate.IsNotNull(nameof(fileContext), fileContext);
            Validate.IsNotNull(nameof(startOffset), startOffset);
            Validate.IsNotNull(nameof(endOffset), endOffset);

            FileContext = fileContext;
            StartOffset = startOffset;
            EndOffset = endOffset;
            BufferRange = fileContext.scriptFile.GetRangeBetweenOffsets(startOffset, endOffset);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Return the text this extent refers to.
        /// </summary>
        public override string ToString()
        {
            return Text;
        }

        /// <summary>
        /// Moves the start and end positions of the extent by an offset. Can
        /// be used to move forwards or backwards.
        /// </summary>
        /// <param name="offset">The amount to move the extent.</param>
        public void AddOffset(int offset) {
            StartOffset += offset;
            EndOffset += offset;

            BufferRange = FileContext.scriptFile.GetRangeBetweenOffsets(StartOffset, EndOffset);
        }

        #endregion
    }
}