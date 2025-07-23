using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Nalix.Cryptography.Symmetric.Block;

/// <summary>
/// Provides Blowfish encryption and decryption functionalities.
/// </summary>
public class Blowfish
{
    #region Constants

    /// <summary>
    /// The number of rounds used in Blowfish encryption.
    /// </summary>
    public const Int32 N = 16;

    private const Int32 KeyMinBytes = 4; // Minimum key size in bytes
    private const Int32 KeyMaxBytes = 56; // Maximum key size in bytes

    #endregion Constants

    #region Fields

    private readonly UInt32[] P;
    private readonly UInt32[,] S;

    private static readonly UInt32[] _P =
    [
        0X243F6A88, 0X85A308D3, 0X13198A2E, 0X03707344, 0XA4093822, 0X299F31D0,
        0X082EFA98, 0XEC4E6C89, 0X452821E6, 0X38D01377, 0XBE5466CF, 0X34E90C6C,
        0XC0AC29B7, 0XC97C50DD, 0X3F84D5B5, 0XB5470917, 0X9216D5D9, 0X8979FB1B
    ];

    private static readonly UInt32[,] _S =
    {
        {
            0XD1310BA6, 0X98DFB5AC, 0X2FFD72DB, 0XD01ADFB7, 0XB8E1AFED, 0X6A267E96,
            0XBA7C9045, 0XF12C7F99, 0X24A19947, 0XB3916CF7, 0X0801F2E2, 0X858EFC16,
            0X636920D8, 0X71574E69, 0XA458FEA3, 0XF4933D7E, 0X0D95748F, 0X728EB658,
            0X718BCD58, 0X82154AEE, 0X7B54A41D, 0XC25A59B5, 0X9C30D539, 0X2AF26013,
            0XC5D1B023, 0X286085F0, 0XCA417918, 0XB8DB38EF, 0X8E79DCB0, 0X603A180E,
            0X6C9E0E8B, 0XB01E8A3E, 0XD71577C1, 0XBD314B27, 0X78AF2FDA, 0X55605C60,
            0XE65525F3, 0XAA55AB94, 0X57489862, 0X63E81440, 0X55CA396A, 0X2AAB10B6,
            0XB4CC5C34, 0X1141E8CE, 0XA15486AF, 0X7C72E993, 0XB3EE1411, 0X636FBC2A,
            0X2BA9C55D, 0X741831F6, 0XCE5C3E16, 0X9B87931E, 0XAFD6BA33, 0X6C24CF5C,
            0X7A325381, 0X28958677, 0X3B8F4898, 0X6B4BB9AF, 0XC4BFE81B, 0X66282193,
            0X61D809CC, 0XFB21A991, 0X487CAC60, 0X5DEC8032, 0XEF845D5D, 0XE98575B1,
            0XDC262302, 0XEB651B88, 0X23893E81, 0XD396ACC5, 0X0F6D6FF3, 0X83F44239,
            0X2E0B4482, 0XA4842004, 0X69C8F04A, 0X9E1F9B5E, 0X21C66842, 0XF6E96C9A,
            0X670C9C61, 0XABD388F0, 0X6A51A0D2, 0XD8542F68, 0X960FA728, 0XAB5133A3,
            0X6EEF0B6C, 0X137A3BE4, 0XBA3BF050, 0X7EFB2A98, 0XA1F1651D, 0X39AF0176,
            0X66CA593E, 0X82430E88, 0X8CEE8619, 0X456F9FB4, 0X7D84A5C3, 0X3B8B5EBE,
            0XE06F75D8, 0X85C12073, 0X401A449F, 0X56C16AA6, 0X4ED3AA62, 0X363F7706,
            0X1BFEDF72, 0X429B023D, 0X37D0D724, 0XD00A1248, 0XDB0FEAD3, 0X49F1C09B,
            0X075372C9, 0X80991B7B, 0X25D479D8, 0XF6E8DEF7, 0XE3FE501A, 0XB6794C3B,
            0X976CE0BD, 0X04C006BA, 0XC1A94FB6, 0X409F60C4, 0X5E5C9EC2, 0X196A2463,
            0X68FB6FAF, 0X3E6C53B5, 0X1339B2EB, 0X3B52EC6F, 0X6DFC511F, 0X9B30952C,
            0XCC814544, 0XAF5EBD09, 0XBEE3D004, 0XDE334AFD, 0X660F2807, 0X192E4BB3,
            0XC0CBA857, 0X45C8740F, 0XD20B5F39, 0XB9D3FBDB, 0X5579C0BD, 0X1A60320A,
            0XD6A100C6, 0X402C7279, 0X679F25FE, 0XFB1FA3CC, 0X8EA5E9F8, 0XDB3222F8,
            0X3C7516DF, 0XFD616B15, 0X2F501EC8, 0XAD0552AB, 0X323DB5FA, 0XFD238760,
            0X53317B48, 0X3E00DF82, 0X9E5C57BB, 0XCA6F8CA0, 0X1A87562E, 0XDF1769DB,
            0XD542A8F6, 0X287EFFC3, 0XAC6732C6, 0X8C4F5573, 0X695B27B0, 0XBBCA58C8,
            0XE1FFA35D, 0XB8F011A0, 0X10FA3D98, 0XFD2183B8, 0X4AFCB56C, 0X2DD1D35B,
            0X9A53E479, 0XB6F84565, 0XD28E49BC, 0X4BFB9790, 0XE1DDF2DA, 0XA4CB7E33,
            0X62FB1341, 0XCEE4C6E8, 0XEF20CADA, 0X36774C01, 0XD07E9EFE, 0X2BF11FB4,
            0X95DBDA4D, 0XAE909198, 0XEAAD8E71, 0X6B93D5A0, 0XD08ED1D0, 0XAFC725E0,
            0X8E3C5B2F, 0X8E7594B7, 0X8FF6E2FB, 0XF2122B64, 0X8888B812, 0X900DF01C,
            0X4FAD5EA0, 0X688FC31C, 0XD1CFF191, 0XB3A8C1AD, 0X2F2F2218, 0XBE0E1777,
            0XEA752DFE, 0X8B021FA1, 0XE5A0CC0F, 0XB56F74E8, 0X18ACF3D6, 0XCE89E299,
            0XB4A84FE0, 0XFD13E0B7, 0X7CC43B81, 0XD2ADA8D9, 0X165FA266, 0X80957705,
            0X93CC7314, 0X211A1477, 0XE6AD2065, 0X77B5FA86, 0XC75442F5, 0XFB9D35CF,
            0XEBCDAF0C, 0X7B3E89A0, 0XD6411BD3, 0XAE1E7E49, 0X00250E2D, 0X2071B35E,
            0X226800BB, 0X57B8E0AF, 0X2464369B, 0XF009B91E, 0X5563911D, 0X59DFA6AA,
            0X78C14389, 0XD95A537F, 0X207D5BA2, 0X02E5B9C5, 0X83260376, 0X6295CFA9,
            0X11C81968, 0X4E734A41, 0XB3472DCA, 0X7B14A94A, 0X1B510052, 0X9A532915,
            0XD60F573F, 0XBC9BC6E4, 0X2B60A476, 0X81E67400, 0X08BA6FB5, 0X571BE91F,
            0XF296EC6B, 0X2A0DD915, 0XB6636521, 0XE7B9F9B6, 0XFF34052E, 0XC5855664,
            0X53B02D5D, 0XA99F8FA1, 0X08BA4799, 0X6E85076A
        },
        {
            0X4B7A70E9, 0XB5B32944, 0XDB75092E, 0XC4192623, 0XAD6EA6B0, 0X49A7DF7D,
            0X9CEE60B8, 0X8FEDB266, 0XECAA8C71, 0X699A17FF, 0X5664526C, 0XC2B19EE1,
            0X193602A5, 0X75094C29, 0XA0591340, 0XE4183A3E, 0X3F54989A, 0X5B429D65,
            0X6B8FE4D6, 0X99F73FD6, 0XA1D29C07, 0XEFE830F5, 0X4D2D38E6, 0XF0255DC1,
            0X4CDD2086, 0X8470EB26, 0X6382E9C6, 0X021ECC5E, 0X09686B3F, 0X3EBAEFC9,
            0X3C971814, 0X6B6A70A1, 0X687F3584, 0X52A0E286, 0XB79C5305, 0XAA500737,
            0X3E07841C, 0X7FDEAE5C, 0X8E7D44EC, 0X5716F2B8, 0XB03ADA37, 0XF0500C0D,
            0XF01C1F04, 0X0200B3FF, 0XAE0CF51A, 0X3CB574B2, 0X25837A58, 0XDC0921BD,
            0XD19113F9, 0X7CA92FF6, 0X94324773, 0X22F54701, 0X3AE5E581, 0X37C2DADC,
            0XC8B57634, 0X9AF3DDA7, 0XA9446146, 0X0FD0030E, 0XECC8C73E, 0XA4751E41,
            0XE238CD99, 0X3BEA0E2F, 0X3280BBA1, 0X183EB331, 0X4E548B38, 0X4F6DB908,
            0X6F420D03, 0XF60A04BF, 0X2CB81290, 0X24977C79, 0X5679B072, 0XBCAF89AF,
            0XDE9A771F, 0XD9930810, 0XB38BAE12, 0XDCCF3F2E, 0X5512721F, 0X2E6B7124,
            0X501ADDE6, 0X9F84CD87, 0X7A584718, 0X7408DA17, 0XBC9F9ABC, 0XE94B7D8C,
            0XEC7AEC3A, 0XDB851DFA, 0X63094366, 0XC464C3D2, 0XEF1C1847, 0X3215D908,
            0XDD433B37, 0X24C2BA16, 0X12A14D43, 0X2A65C451, 0X50940002, 0X133AE4DD,
            0X71DFF89E, 0X10314E55, 0X81AC77D6, 0X5F11199B, 0X043556F1, 0XD7A3C76B,
            0X3C11183B, 0X5924A509, 0XF28FE6ED, 0X97F1FBFA, 0X9EBABF2C, 0X1E153C6E,
            0X86E34570, 0XEAE96FB1, 0X860E5E0A, 0X5A3E2AB3, 0X771FE71C, 0X4E3D06FA,
            0X2965DCB9, 0X99E71D0F, 0X803E89D6, 0X5266C825, 0X2E4CC978, 0X9C10B36A,
            0XC6150EBA, 0X94E2EA78, 0XA5FC3C53, 0X1E0A2DF4, 0XF2F74EA7, 0X361D2B3D,
            0X1939260F, 0X19C27960, 0X5223A708, 0XF71312B6, 0XEBADFE6E, 0XEAC31F66,
            0XE3BC4595, 0XA67BC883, 0XB17F37D1, 0X018CFF28, 0XC332DDEF, 0XBE6C5AA5,
            0X65582185, 0X68AB9802, 0XEECEA50F, 0XDB2F953B, 0X2AEF7DAD, 0X5B6E2F84,
            0X1521B628, 0X29076170, 0XECDD4775, 0X619F1510, 0X13CCA830, 0XEB61BD96,
            0X0334FE1E, 0XAA0363CF, 0XB5735C90, 0X4C70A239, 0XD59E9E0B, 0XCBAADE14,
            0XEECC86BC, 0X60622CA7, 0X9CAB5CAB, 0XB2F3846E, 0X648B1EAF, 0X19BDF0CA,
            0XA02369B9, 0X655ABB50, 0X40685A32, 0X3C2AB4B3, 0X319EE9D5, 0XC021B8F7,
            0X9B540B19, 0X875FA099, 0X95F7997E, 0X623D7DA8, 0XF837889A, 0X97E32D77,
            0X11ED935F, 0X16681281, 0X0E358829, 0XC7E61FD6, 0X96DEDFA1, 0X7858BA99,
            0X57F584A5, 0X1B227263, 0X9B83C3FF, 0X1AC24696, 0XCDB30AEB, 0X532E3054,
            0X8FD948E4, 0X6DBC3128, 0X58EBF2EF, 0X34C6FFEA, 0XFE28ED61, 0XEE7C3C73,
            0X5D4A14D9, 0XE864B7E3, 0X42105D14, 0X203E13E0, 0X45EEE2B6, 0XA3AAABEA,
            0XDB6C4F15, 0XFACB4FD0, 0XC742F442, 0XEF6ABBB5, 0X654F3B1D, 0X41CD2105,
            0XD81E799E, 0X86854DC7, 0XE44B476A, 0X3D816250, 0XCF62A1F2, 0X5B8D2646,
            0XFC8883A0, 0XC1C7B6A3, 0X7F1524C3, 0X69CB7492, 0X47848A0B, 0X5692B285,
            0X095BBF00, 0XAD19489D, 0X1462B174, 0X23820E00, 0X58428D2A, 0X0C55F5EA,
            0X1DADF43E, 0X233F7061, 0X3372F092, 0X8D937E41, 0XD65FECF1, 0X6C223BDB,
            0X7CDE3759, 0XCBEE7460, 0X4085F2A7, 0XCE77326E, 0XA6078084, 0X19F8509E,
            0XE8EFD855, 0X61D99735, 0XA969A7AA, 0XC50C06C2, 0X5A04ABFC, 0X800BCADC,
            0X9E447A2E, 0XC3453484, 0XFDD56705, 0X0E1E9EC9, 0XDB73DBD3, 0X105588CD,
            0X675FDA79, 0XE3674340, 0XC5C43465, 0X713E38D8, 0X3D28F89E, 0XF16DFF20,
            0X153E21E7, 0X8FB03D4A, 0XE6E39F2B, 0XDB83ADF7
        },
        {
            0XE93D5A68, 0X948140F7, 0XF64C261C, 0X94692934, 0X411520F7, 0X7602D4F7,
            0XBCF46B2E, 0XD4A20068, 0XD4082471, 0X3320F46A, 0X43B7D4B7, 0X500061AF,
            0X1E39F62E, 0X97244546, 0X14214F74, 0XBF8B8840, 0X4D95FC1D, 0X96B591AF,
            0X70F4DDD3, 0X66A02F45, 0XBFBC09EC, 0X03BD9785, 0X7FAC6DD0, 0X31CB8504,
            0X96EB27B3, 0X55FD3941, 0XDA2547E6, 0XABCA0A9A, 0X28507825, 0X530429F4,
            0X0A2C86DA, 0XE9B66DFB, 0X68DC1462, 0XD7486900, 0X680EC0A4, 0X27A18DEE,
            0X4F3FFEA2, 0XE887AD8C, 0XB58CE006, 0X7AF4D6B6, 0XAACE1E7C, 0XD3375FEC,
            0XCE78A399, 0X406B2A42, 0X20FE9E35, 0XD9F385B9, 0XEE39D7AB, 0X3B124E8B,
            0X1DC9FAF7, 0X4B6D1856, 0X26A36631, 0XEAE397B2, 0X3A6EFA74, 0XDD5B4332,
            0X6841E7F7, 0XCA7820FB, 0XFB0AF54E, 0XD8FEB397, 0X454056AC, 0XBA489527,
            0X55533A3A, 0X20838D87, 0XFE6BA9B7, 0XD096954B, 0X55A867BC, 0XA1159A58,
            0XCCA92963, 0X99E1DB33, 0XA62A4A56, 0X3F3125F9, 0X5EF47E1C, 0X9029317C,
            0XFDF8E802, 0X04272F70, 0X80BB155C, 0X05282CE3, 0X95C11548, 0XE4C66D22,
            0X48C1133F, 0XC70F86DC, 0X07F9C9EE, 0X41041F0F, 0X404779A4, 0X5D886E17,
            0X325F51EB, 0XD59BC0D1, 0XF2BCC18F, 0X41113564, 0X257B7834, 0X602A9C60,
            0XDFF8E8A3, 0X1F636C1B, 0X0E12B4C2, 0X02E1329E, 0XAF664FD1, 0XCAD18115,
            0X6B2395E0, 0X333E92E1, 0X3B240B62, 0XEEBEB922, 0X85B2A20E, 0XE6BA0D99,
            0XDE720C8C, 0X2DA2F728, 0XD0127845, 0X95B794FD, 0X647D0862, 0XE7CCF5F0,
            0X5449A36F, 0X877D48FA, 0XC39DFD27, 0XF33E8D1E, 0X0A476341, 0X992EFF74,
            0X3A6F6EAB, 0XF4F8FD37, 0XA812DC60, 0XA1EBDDF8, 0X991BE14C, 0XDB6E6B0D,
            0XC67B5510, 0X6D672C37, 0X2765D43B, 0XDCD0E804, 0XF1290DC7, 0XCC00FFA3,
            0XB5390F92, 0X690FED0B, 0X667B9FFB, 0XCEDB7D9C, 0XA091CF0B, 0XD9155EA3,
            0XBB132F88, 0X515BAD24, 0X7B9479BF, 0X763BD6EB, 0X37392EB3, 0XCC115979,
            0X8026E297, 0XF42E312D, 0X6842ADA7, 0XC66A2B3B, 0X12754CCC, 0X782EF11C,
            0X6A124237, 0XB79251E7, 0X06A1BBE6, 0X4BFB6350, 0X1A6B1018, 0X11CAEDFA,
            0X3D25BDD8, 0XE2E1C3C9, 0X44421659, 0X0A121386, 0XD90CEC6E, 0XD5ABEA2A,
            0X64AF674E, 0XDA86A85F, 0XBEBFE988, 0X64E4C3FE, 0X9DBC8057, 0XF0F7C086,
            0X60787BF8, 0X6003604D, 0XD1FD8346, 0XF6381FB0, 0X7745AE04, 0XD736FCCC,
            0X83426B33, 0XF01EAB71, 0XB0804187, 0X3C005E5F, 0X77A057BE, 0XBDE8AE24,
            0X55464299, 0XBF582E61, 0X4E58F48F, 0XF2DDFDA2, 0XF474EF38, 0X8789BDC2,
            0X5366F9C3, 0XC8B38E74, 0XB475F255, 0X46FCD9B9, 0X7AEB2661, 0X8B1DDF84,
            0X846A0E79, 0X915F95E2, 0X466E598E, 0X20B45770, 0X8CD55591, 0XC902DE4C,
            0XB90BACE1, 0XBB8205D0, 0X11A86248, 0X7574A99E, 0XB77F19B6, 0XE0A9DC09,
            0X662D09A1, 0XC4324633, 0XE85A1F02, 0X09F0BE8C, 0X4A99A025, 0X1D6EFE10,
            0X1AB93D1D, 0X0BA5A4DF, 0XA186F20F, 0X2868F169, 0XDCB7DA83, 0X573906FE,
            0XA1E2CE9B, 0X4FCD7F52, 0X50115E01, 0XA70683FA, 0XA002B5C4, 0X0DE6D027,
            0X9AF88C27, 0X773F8641, 0XC3604C06, 0X61A806B5, 0XF0177A28, 0XC0F586E0,
            0X006058AA, 0X30DC7D62, 0X11E69ED7, 0X2338EA63, 0X53C2DD94, 0XC2C21634,
            0XBBCBEE56, 0X90BCB6DE, 0XEBFC7DA1, 0XCE591D76, 0X6F05E409, 0X4B7C0188,
            0X39720A3D, 0X7C927C24, 0X86E3725F, 0X724D9DB9, 0X1AC15BB4, 0XD39EB8FC,
            0XED545578, 0X08FCA5B5, 0XD83D7CD3, 0X4DAD0FC4, 0X1E50EF5E, 0XB161E6F8,
            0XA28514D9, 0X6C51133C, 0X6FD5C7E7, 0X56E14EC4, 0X362ABFCE, 0XDDC6C837,
            0XD79A3234, 0X92638212, 0X670EFA8E, 0X406000E0
        },
        {
            0X3A39CE37, 0XD3FAF5CF, 0XABC27737, 0X5AC52D1B, 0X5CB0679E, 0X4FA33742,
            0XD3822740, 0X99BC9BBE, 0XD5118E9D, 0XBF0F7315, 0XD62D1C7E, 0XC700C47B,
            0XB78C1B6B, 0X21A19045, 0XB26EB1BE, 0X6A366EB4, 0X5748AB2F, 0XBC946E79,
            0XC6A376D2, 0X6549C2C8, 0X530FF8EE, 0X468DDE7D, 0XD5730A1D, 0X4CD04DC6,
            0X2939BBDB, 0XA9BA4650, 0XAC9526E8, 0XBE5EE304, 0XA1FAD5F0, 0X6A2D519A,
            0X63EF8CE2, 0X9A86EE22, 0XC089C2B8, 0X43242EF6, 0XA51E03AA, 0X9CF2D0A4,
            0X83C061BA, 0X9BE96A4D, 0X8FE51550, 0XBA645BD6, 0X2826A2F9, 0XA73A3AE1,
            0X4BA99586, 0XEF5562E9, 0XC72FEFD3, 0XF752F7DA, 0X3F046F69, 0X77FA0A59,
            0X80E4A915, 0X87B08601, 0X9B09E6AD, 0X3B3EE593, 0XE990FD5A, 0X9E34D797,
            0X2CF0B7D9, 0X022B8B51, 0X96D5AC3A, 0X017DA67D, 0XD1CF3ED6, 0X7C7D2D28,
            0X1F9F25CF, 0XADF2B89B, 0X5AD6B472, 0X5A88F54C, 0XE029AC71, 0XE019A5E6,
            0X47B0ACFD, 0XED93FA9B, 0XE8D3C48D, 0X283B57CC, 0XF8D56629, 0X79132E28,
            0X785F0191, 0XED756055, 0XF7960E44, 0XE3D35E8C, 0X15056DD4, 0X88F46DBA,
            0X03A16125, 0X0564F0BD, 0XC3EB9E15, 0X3C9057A2, 0X97271AEC, 0XA93A072A,
            0X1B3F6D9B, 0X1E6321F5, 0XF59C66FB, 0X26DCF319, 0X7533D928, 0XB155FDF5,
            0X03563482, 0X8ABA3CBB, 0X28517711, 0XC20AD9F8, 0XABCC5167, 0XCCAD925F,
            0X4DE81751, 0X3830DC8E, 0X379D5862, 0X9320F991, 0XEA7A90C2, 0XFB3E7BCE,
            0X5121CE64, 0X774FBE32, 0XA8B6E37E, 0XC3293D46, 0X48DE5369, 0X6413E680,
            0XA2AE0810, 0XDD6DB224, 0X69852DFD, 0X09072166, 0XB39A460A, 0X6445C0DD,
            0X586CDECF, 0X1C20C8AE, 0X5BBEF7DD, 0X1B588D40, 0XCCD2017F, 0X6BB4E3BB,
            0XDDA26A7E, 0X3A59FF45, 0X3E350A44, 0XBCB4CDD5, 0X72EACEA8, 0XFA6484BB,
            0X8D6612AE, 0XBF3C6F47, 0XD29BE463, 0X542F5D9E, 0XAEC2771B, 0XF64E6370,
            0X740E0D8D, 0XE75B1357, 0XF8721671, 0XAF537D5D, 0X4040CB08, 0X4EB4E2CC,
            0X34D2466A, 0X0115AF84, 0XE1B00428, 0X95983A1D, 0X06B89FB4, 0XCE6EA048,
            0X6F3F3B82, 0X3520AB82, 0X011A1D4B, 0X277227F8, 0X611560B1, 0XE7933FDC,
            0XBB3A792B, 0X344525BD, 0XA08839E1, 0X51CE794B, 0X2F32C9B7, 0XA01FBAC9,
            0XE01CC87E, 0XBCC7D1F6, 0XCF0111C3, 0XA1E8AAC7, 0X1A908749, 0XD44FBD9A,
            0XD0DADECB, 0XD50ADA38, 0X0339C32A, 0XC6913667, 0X8DF9317C, 0XE0B12B4F,
            0XF79E59B7, 0X43F5BB3A, 0XF2D519FF, 0X27D9459C, 0XBF97222C, 0X15E6FC2A,
            0X0F91FC71, 0X9B941525, 0XFAE59361, 0XCEB69CEB, 0XC2A86459, 0X12BAA8D1,
            0XB6C1075E, 0XE3056A0C, 0X10D25065, 0XCB03A442, 0XE0EC6E0E, 0X1698DB3B,
            0X4C98A0BE, 0X3278E964, 0X9F1F9532, 0XE0D392DF, 0XD3A0342B, 0X8971F21E,
            0X1B0A7441, 0X4BA3348C, 0XC5BE7120, 0XC37632D8, 0XDF359F8D, 0X9B992F2E,
            0XE60B6F47, 0X0FE3F11D, 0XE54CDA54, 0X1EDAD891, 0XCE6279CF, 0XCD3E7E6F,
            0X1618B166, 0XFD2C1D05, 0X848FD2C5, 0XF6FB2299, 0XF523F357, 0XA6327623,
            0X93A83531, 0X56CCCD02, 0XACF08162, 0X5A75EBB5, 0X6E163697, 0X88D273CC,
            0XDE966292, 0X81B949D0, 0X4C50901B, 0X71C65614, 0XE6C6C7BD, 0X327A140A,
            0X45E1D006, 0XC3F27B9A, 0XC9AA53FD, 0X62A80F00, 0XBB25BFE2, 0X35BDD2F6,
            0X71126905, 0XB2040222, 0XB6CBCF7C, 0XCD769C2B, 0X53113EC0, 0X1640E3D3,
            0X38ABBD60, 0X2547ADF0, 0XBA38209C, 0XF746CE76, 0X77AFA1C5, 0X20756060,
            0X85CBFE4E, 0X8AE88DD8, 0X7AAAF9B0, 0X4CF9AA7E, 0X1948C25C, 0X02FB8A8C,
            0X01C36AE4, 0XD6EBE1F9, 0X90D4F869, 0XA65CDEA0, 0X3F09252D, 0XC208E69F,
            0XB74E6132, 0XCE77E25B, 0X578FDFE3, 0X3AC372E6
        }
    };

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Blowfish"/> class with the specified key.
    /// </summary>
    /// <param name="schedule"></param>
    /// <param name="key"></param>
    public Blowfish(UInt32[] schedule, Byte[] key)
    {
        P = new UInt32[18];
        S = new UInt32[4, 256];
        Buffer.BlockCopy(schedule, 0, P, 0, 18 * 4);
        Buffer.BlockCopy(schedule, 18 * 4, S, 0, 1024 * 4);
        InitializeKeySchedule(key);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Blowfish"/> class with the specified key.
    /// </summary>
    /// <param name="key">The encryption key (must be between 4 and 56 bytes).</param>
    public Blowfish(Byte[] key)
    {
        P = _P.Clone() as UInt32[];
        S = _S.Clone() as UInt32[,];
        InitializeKeySchedule(key);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Blowfish"/> class with a string key.
    /// </summary>
    /// <param name="key">The encryption key as a string.</param>
    public Blowfish(String key)
        : this(Encoding.ASCII.GetBytes(key))
    {
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>
    /// Encrypts the given data in place.
    /// </summary>
    /// <param name="data">The byte array to encrypt.</param>
    /// <param name="length">The ProtocolType of bytes to encrypt (must be a multiple of 8).</param>
    /// <exception cref="ArgumentException">Thrown if the length is not a multiple of 8.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EncryptInPlace(Byte[] data, Int32 length) => this.EncryptBlock(data, 0, length);

    /// <summary>
    /// Encrypts a specific block of data in place.
    /// </summary>
    /// <param name="data">The byte array to encrypt.</param>
    /// <param name="offset">The starting position in the array.</param>
    /// <param name="length">The ProtocolType of bytes to encrypt (must be a multiple of 8).</param>
    /// <exception cref="ArgumentException">Thrown if the length is not a multiple of 8.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EncryptBlock(Byte[] data, Int32 offset, Int32 length)
    {
        if (length % 8 != 0)
        {
            throw new ArgumentException("Length must be a multiple of 8.", nameof(length));
        }

        for (Int32 i = offset; i < offset + length; i += 8)
        {
            UInt32 left = BitConverter.ToUInt32(data, i);
            UInt32 right = BitConverter.ToUInt32(data, i + 4);
            this.EncryptBlock(ref left, ref right);

            Buffer.BlockCopy(BitConverter.GetBytes(left), 0, data, i, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(right), 0, data, i + 4, 4);
        }
    }

    /// <summary>
    /// Encrypts a string and returns a Base64Value-encoded result.
    /// </summary>
    /// <param name="plainText">The plaintext string to encrypt.</param>
    /// <returns>A Base64Value-encoded encrypted string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public String EncryptToBase64(String plainText)
    {
        Byte[] data = Encoding.Unicode.GetBytes(plainText);
        this.EncryptInPlace(data, data.Length);
        return Convert.ToBase64String(data);
    }

    /// <summary>
    /// Decrypts the given data in place.
    /// </summary>
    /// <param name="data">The byte array to decrypt.</param>
    /// <param name="length">The ProtocolType of bytes to decrypt (must be a multiple of 8).</param>
    /// <exception cref="ArgumentException">Thrown if the length is not a multiple of 8.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecryptInPlace(Byte[] data, Int32 length) => this.DecryptBlock(data, 0, length);

    /// <summary>
    /// Decrypts a specific block of data in place.
    /// </summary>
    /// <param name="data">The byte array to decrypt.</param>
    /// <param name="offset">The starting position in the array.</param>
    /// <param name="length">The ProtocolType of bytes to decrypt (must be a multiple of 8).</param>
    /// <exception cref="ArgumentException">Thrown if the length is not a multiple of 8.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DecryptBlock(Byte[] data, Int32 offset, Int32 length)
    {
        if (length % 8 != 0)
        {
            throw new ArgumentException("Length must be a multiple of 8.", nameof(length));
        }

        for (Int32 i = offset; i < offset + length; i += 8)
        {
            UInt32 left = BitConverter.ToUInt32(data, i);
            UInt32 right = BitConverter.ToUInt32(data, i + 4);
            this.DecryptBlock(ref left, ref right);

            Buffer.BlockCopy(BitConverter.GetBytes(left), 0, data, i, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(right), 0, data, i + 4, 4);
        }
    }

    /// <summary>
    /// Decrypts a Base64Value-encoded string.
    /// </summary>
    /// <param name="cipherText">The Base64Value-encoded encrypted string.</param>
    /// <returns>The decrypted plaintext string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public String DecryptFromBase64(String cipherText)
    {
        Byte[] data = Convert.FromBase64String(cipherText);
        this.DecryptInPlace(data, data.Length);
        return Encoding.Unicode.GetString(data);
    }

    #endregion Public Methods

    #region Private Methods

    /// <summary>
    /// Encrypts a single 8-byte block.
    /// </summary>
    /// <param name="left">The left 32-bit half of the block.</param>
    /// <param name="right">The right 32-bit half of the block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EncryptBlock(ref UInt32 left, ref UInt32 right)
    {
        for (Int32 i = 0; i < N; ++i)
        {
            left ^= P[i];
            right ^= this.SubstitutionFunction(left);
            (left, right) = (right, left); // Swap halves
        }

        (left, right) = (right, left); // Undo final swap

        right ^= P[N];
        left ^= P[N + 1];
    }

    /// <summary>
    /// Decrypts a single 8-byte block.
    /// </summary>
    /// <param name="left">The left 32-bit half of the block.</param>
    /// <param name="right">The right 32-bit half of the block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecryptBlock(ref UInt32 left, ref UInt32 right)
    {
        for (Int32 i = N + 1; i > 1; --i)
        {
            left ^= P[i];
            right ^= this.SubstitutionFunction(left);
            (left, right) = (right, left); // Swap halves
        }

        (left, right) = (right, left); // Undo final swap

        right ^= P[1];
        left ^= P[0];
    }

    /// <summary>
    /// Blowfish substitution function (F function).
    /// </summary>
    /// <param name="value">The 32-bit input value.</param>
    /// <returns>The transformed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private UInt32 SubstitutionFunction(UInt32 value)
    {
        UInt16 a = (UInt16)((value >> 24) & 0xFF);
        UInt16 b = (UInt16)((value >> 16) & 0xFF);
        UInt16 c = (UInt16)((value >> 8) & 0xFF);
        UInt16 d = (UInt16)(value & 0xFF);

        return (S[0, a] + S[1, b]) ^ (S[2, c] + S[3, d]);
    }

    /// <summary>
    /// Initializes the key schedule using the provided key.
    /// </summary>
    /// <param name="key">The encryption key.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InitializeKeySchedule(ReadOnlySpan<Byte> key)
    {
        // Validate key length
        if (key.Length is < KeyMinBytes or > KeyMaxBytes)
        {
            throw new ArgumentException($"Key length must be between {KeyMinBytes} and {KeyMaxBytes} bytes.");
        }

        Int16 j = 0;
        UInt32 data = 0;

        // XOR P-array with key material
        for (Int16 i = 0; i < N + 2; ++i)
        {
            for (Int16 k = 0; k < 4; ++k)
            {
                data = (data << 8) | key[j++];
                if (j >= key.Length)
                {
                    j = 0;
                }
            }
            P[i] ^= data;
        }
    }

    #endregion Private Methods
}
