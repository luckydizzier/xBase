using System;
using System.Collections.Generic;
using System.Text;

namespace XBase.Core.Table;

internal static class DbfEncodingRegistry
{
  private static readonly IReadOnlyDictionary<byte, int> CodePageByLanguageDriverId =
    new Dictionary<byte, int>
    {
      { 0x00, 437 },  // Undefined (defaults to US MS-DOS)
      { 0x01, 437 },   // US MS-DOS
      { 0x02, 850 },   // International MS-DOS
      { 0x03, 1252 },  // Windows ANSI
      { 0x57, 1252 },  // ANSI (dBASE IV)
      { 0x64, 852 },   // Eastern European MS-DOS
      { 0x65, 857 },   // Turkish MS-DOS
      { 0x66, 866 },   // Russian MS-DOS
      { 0x67, 865 },   // Nordic MS-DOS
      { 0x68, 861 },   // Icelandic MS-DOS
      { 0x69, 895 },   // Czech MS-DOS
      { 0x6A, 620 },   // Mazovia (Polish) MS-DOS
      { 0x6B, 737 },   // Greek MS-DOS
      { 0x78, 950 },   // Chinese (Traditional) Windows/DOS
      { 0x79, 949 },   // Korean Windows/DOS
      { 0x7A, 936 },   // Chinese (Simplified) Windows/DOS
      { 0x7B, 932 },   // Japanese Windows/DOS
      { 0x7C, 874 },   // Thai Windows
      { 0x7D, 1255 },  // Hebrew Windows
      { 0x7E, 1256 },  // Arabic Windows
      { 0x96, 10007 }, // Russian Macintosh
      { 0x97, 10029 }, // Mac Latin 2
      { 0x98, 10006 }, // Mac Greek I
      { 0x99, 10081 }, // Mac Turkish
      { 0xC8, 1250 },  // Eastern European Windows
      { 0xC9, 1251 },  // Russian Windows
      { 0xCA, 1254 },  // Turkish Windows
      { 0xCB, 1253 },  // Greek Windows
      { 0xCC, 1257 },  // Baltic Windows
      { 0xCD, 1252 },  // Windows ANSI (FoxPro)
    };

  private static readonly Encoding DefaultEncoding;

  static DbfEncodingRegistry()
  {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    if (!CodePageByLanguageDriverId.TryGetValue(0x00, out int fallbackCodePage))
    {
      fallbackCodePage = 437;
    }

    DefaultEncoding = Encoding.GetEncoding(fallbackCodePage);
  }

  public static Encoding Resolve(byte languageDriverId)
  {
    if (CodePageByLanguageDriverId.TryGetValue(languageDriverId, out int codePage))
    {
      try
      {
        return Encoding.GetEncoding(codePage);
      }
      catch (ArgumentException)
      {
      }
      catch (NotSupportedException)
      {
      }
    }

    return DefaultEncoding;
  }
}
