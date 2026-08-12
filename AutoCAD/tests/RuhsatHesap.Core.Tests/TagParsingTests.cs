using RuhsatHesap.Core;
using RuhsatHesap.Core.Tagging;
using Xunit;

namespace RuhsatHesap.Core.Tests
{
    public class TagParsingTests
    {
        [Fact]
        public void ParsesLongForm ()
        {
            RuhsatTag tag = RuhsatTag.Parse ("RH|BLOK=A|BB=01|KAT=ZEMİN KAT|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=Mesken");

            Assert.True (tag.IsRuhsatTag);
            Assert.True (tag.Valid);
            Assert.Equal ("A", tag.BlockName);
            Assert.Equal ("01", tag.UnitNumber);
            Assert.Equal ("ZEMİN KAT", tag.FloorName);
            Assert.Equal (AreaKind.Net, tag.Kind);
            Assert.Equal (3, tag.RoomCount);
            Assert.Equal ("SALON", tag.RoomName);
            Assert.Equal ("Mesken", tag.Quality);
            Assert.True (tag.IsUnitArea);
        }

        [Fact]
        public void AcceptsCompactAliasesAndColons()
        {
            RuhsatTag tag = RuhsatTag.Parse ("rh|b:A|bb:2|k:1. KAT|t:BRÜT|o:2");

            Assert.True (tag.Valid);
            Assert.Equal ("A", tag.BlockName);
            Assert.Equal ("2", tag.UnitNumber);
            Assert.Equal ("1. KAT", tag.FloorName);
            Assert.Equal (AreaKind.Gross, tag.Kind);
            Assert.Equal (2, tag.RoomCount);
        }

        [Fact]
        public void NonRuhsatTextIsIgnored ()
        {
            RuhsatTag tag = RuhsatTag.Parse ("SALON 24.50 m2");
            Assert.False (tag.IsRuhsatTag);
            Assert.False (tag.Valid);
        }

        [Fact]
        public void HesapEmsalRoutesSupportedTypesOnly ()
        {
            RuhsatTag stair = RuhsatTag.Parse ("RH|BLOK=A|KAT=ZEMIN|TIP=MERDIVEN|HESAP=EMSAL");
            Assert.True (stair.ToThirtyPercentTable);
            Assert.True (RuhsatTag.SupportsHesapEmsal (stair.Kind));

            RuhsatTag shelter = RuhsatTag.Parse ("RH|BLOK=A|KAT=BODRUM|TIP=SIGINAK|HESAP=EMSAL");
            Assert.False (RuhsatTag.SupportsHesapEmsal (shelter.Kind));
        }

        [Fact]
        public void MissingBlockIsReported ()
        {
            RuhsatTag tag = RuhsatTag.Parse ("RH|BB=01|TIP=NET");
            Assert.True (tag.IsRuhsatTag);
            Assert.False (tag.Valid);
            Assert.Equal ("BLOK eksik", tag.Error);
        }

        [Fact]
        public void ParcelLevelTypesNeedNoBlock ()
        {
            RuhsatTag parcel = RuhsatTag.Parse ("RH|TIP=PARSEL");
            Assert.True (parcel.Valid);
            Assert.Equal (AreaKind.ParcelBoundary, parcel.Kind);

            RuhsatTag wall = RuhsatTag.Parse ("RH|TIP=ISTINAT|AD=Doğu Duvarı");
            Assert.True (wall.Valid);
            Assert.Equal (AreaKind.RetainingWall, wall.Kind);
            Assert.Equal ("Doğu Duvarı", wall.Label);

            RuhsatTag structure = RuhsatTag.Parse ("RH|TIP=EK_YAPI|AD=Foseptik");
            Assert.True (structure.Valid);
            Assert.Equal (AreaKind.ExtraStructure, structure.Kind);
            Assert.Equal ("Foseptik", structure.Label);
            Assert.False (structure.NeedsBlock);
            Assert.False (structure.NeedsFloor);
        }

        [Theory]
        [InlineData ("EK_YAPI")]
        [InlineData ("EKYAPI")]
        [InlineData ("EKSTRA")]
        [InlineData ("EKSTRA_YAPI")]
        public void ExtraStructureAliasesAllParse (string typeSpelling)
        {
            RuhsatTag tag = RuhsatTag.Parse ("RH|TIP=" + typeSpelling + "|AD=Trafo");
            Assert.Equal (AreaKind.ExtraStructure, tag.Kind);
            Assert.True (tag.Valid);
        }

        [Fact]
        public void UnknownTypeStaysUnknownUntilSync ()
        {
            RuhsatTag tag = RuhsatTag.Parse ("RH|BLOK=A|KAT=ZEMIN|TIP=HAVUZ_KENARI");
            Assert.Equal (AreaKind.Unknown, tag.Kind);
            Assert.Equal ("HAVUZ_KENARI", tag.AreaTypeName);
            // A free TIP is still a valid etiket; TagSync resolves it against
            // the project's own area columns.
            Assert.True (tag.Valid);
        }

        [Fact]
        public void FormatRoundTrips ()
        {
            const string text = "RH|BLOK=A|BB=01|KAT=1. KAT|TIP=NET|ODA=3|MAHAL=SALON|NITELIK=Mesken";
            RuhsatTag tag = RuhsatTag.Parse (text);
            string formatted = tag.Format ();

            RuhsatTag reparsed = RuhsatTag.Parse (formatted);
            Assert.Equal (tag.BlockName, reparsed.BlockName);
            Assert.Equal (tag.UnitNumber, reparsed.UnitNumber);
            Assert.Equal (tag.FloorName, reparsed.FloorName);
            Assert.Equal (tag.Kind, reparsed.Kind);
            Assert.Equal (tag.RoomCount, reparsed.RoomCount);
            Assert.Equal (tag.Quality, reparsed.Quality);
        }

        [Theory]
        [InlineData ("2. BODRUM", "1. BODRUM")]
        [InlineData ("1. BODRUM", "ZEMİN KAT")]
        [InlineData ("ZEMİN KAT", "ASMA KAT")]
        [InlineData ("ASMA KAT", "1. KAT")]
        [InlineData ("1. KAT", "3. KAT")]
        [InlineData ("3. KAT", "ÇATI KATI")]
        public void FloorsAreOrderedFromTheirNames (string lower, string upper)
        {
            Assert.True (FloorOrder.Rank (lower) < FloorOrder.Rank (upper),
                lower + " < " + upper + " bekleniyordu");
        }

        [Theory]
        [InlineData ("RH-BLOK_A-BB_01-TIP_NET", "RH|BLOK=A|BB=01|TIP=NET")]
        [InlineData ("RH-BLOK_A-TIP_EKLENTI_NET", "RH|BLOK=A|TIP=EKLENTI_NET")]
        [InlineData ("rh-blok_A-kat_1. KAT-tip_BALKON", "RH|blok=A|kat=1. KAT|tip=BALKON")]
        public void LayerNamesMapToTags (string layerName, string expected)
        {
            Assert.True (TagText.TryParseLayerName (layerName, out string tagText));
            Assert.Equal (expected, tagText);
            Assert.True (RuhsatTag.Parse (tagText).IsRuhsatTag);
        }

        [Theory]
        [InlineData ("KAT PLANI")]
        [InlineData ("0")]
        [InlineData ("RHSOMETHING")]
        public void OtherLayerNamesAreNotTags (string layerName)
        {
            Assert.False (TagText.TryParseLayerName (layerName, out string unused));
        }

        [Fact]
        public void MTextFormattingIsStripped ()
        {
            string stripped = TagText.StripMTextFormatting ("{\\fArial|b1|i0|c162|p34;RH|BLOK=A|BB=01|TIP=NET}");
            RuhsatTag tag = RuhsatTag.Parse (stripped);
            Assert.True (tag.Valid);
            Assert.Equal ("01", tag.UnitNumber);
            Assert.Equal (AreaKind.Net, tag.Kind);
        }

        [Theory]
        [InlineData ("RH|BLOK=A|TIP=NET", true)]
        [InlineData ("RH BLOK A", true)]
        [InlineData ("SALON", false)]
        [InlineData ("", false)]
        public void AnnotationTextIsRecognised (string text, bool expected)
        {
            Assert.Equal (expected, TagText.LooksLikeTag (text));
        }
    }
}
