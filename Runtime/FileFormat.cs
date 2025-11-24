
using System;


namespace LDraw
{
    [Flags]
    public enum FileFormat
    {
        Unknown = 0,
        MPD = 1,
        LDR = 2,
        DAT = 4,
        All = MPD | LDR | DAT
    }

    /// <summary>
    /// Line types found in LDraw files.
    /// </summary>
    /// <remarks>
    /// See https://www.ldraw.org/article/218.html#linetypes for more information.
    /// </remarks>
    public enum LineType{
        /// <summary>
        /// A comment or META command.
        /// </summary>
        /// <details>
        /// Lines beginning with 0 are comments or META commands.
        /// They may contain special instructions for rendering
        /// or building the model, but do not represent geometric
        /// primitives.
        /// 
        /// For example, a line starting with "0 BFC CERTIFY" indicates
        /// that the model uses Back Face Culling and certifies that
        /// the faces are oriented correctly.
        /// </details>
        Comment = 0,

        /// <summary>
        /// A sub-file reference.
        /// </summary>
        SubFile = 1,

        /// <summary>
        /// A line primitive.
        /// </summary>
        Line = 2,

        /// <summary>
        /// A triangle primitive.
        /// </summary>
        Triangle = 3,

        /// <summary>
        /// A quadrilateral primitive.
        /// </summary>
        Quadrilateral = 4,

        /// <summary>
        /// An optional line primitive.
        /// </summary>
        OptionalLine = 5,
    }

    public enum BfcOption
    {
        None,
        Certify, // 0 BFC CERTIFY
        NoCertify, // 0 BFC NOCERTIFY
        Clip, // 0 BFC CLIP
        NoClip, // 0 BFC NOCLIP
        CW, // 0 BFC CW
        CCW, // 0 BFC CCW
        InvertNext, // 0 BFC INVERTNEXT
    }
}