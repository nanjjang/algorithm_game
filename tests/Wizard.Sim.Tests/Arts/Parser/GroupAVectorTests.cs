using Wizard.Content;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // spec 03 18.1 A군 117건. 각 줄은 명세 표의 한 행이다(캐릭터, 해금, 결정/빚, 입력열, 결과, 판정 뒤 결정/빚).
    // 입력열의 수는 직전 입력과의 간격(입력 시각 단위, 명세의 괄호 값)이다. 하네스 규칙은 ParserRig.RunA(spec 03 18절 머리).
    public sealed class GroupAVectorTests
    {
        [Fact]
        public void S03_TV_A001__Char1_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 3, 0);

        [Fact]
        public void S03_TV_A002__Char2_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 3, 0);

        [Fact]
        public void S03_TV_A003__Char3_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.F(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 2, 0);

        [Fact]
        public void S03_TV_A004__Char4_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.F(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 4, 0);

        [Fact]
        public void S03_TV_A005__Char5_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.F(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 5, 0);

        [Fact]
        public void S03_TV_A006__Char3_Fire_CopyCircle() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.F(2304), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.CopyCircle), 2, 0);

        [Fact]
        public void S03_TV_A007__Char3_Fire_CopyCircle() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.F(2304), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.CopyCircle), 2, 0);

        [Fact]
        public void S03_TV_A008__Char1_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.B(0), In.B(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Vent), 3, 0);

        [Fact]
        public void S03_TV_A009__Char5_Fire_Afterimage() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.L(0), In.R(2304), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.Afterimage), 5, 0);

        [Fact]
        public void S03_TV_A010__Char5_Fire_Afterimage() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.R(0), In.L(2304), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.Afterimage), 5, 0);

        [Fact]
        public void S03_TV_A011__Char1_Fire_Kiln() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Kiln), 3, 0);

        [Fact]
        public void S03_TV_A012__Char2_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 4, 0);

        [Fact]
        public void S03_TV_A013__Char3_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 3, 0);

        [Fact]
        public void S03_TV_A014__Char4_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 5, 0);

        [Fact]
        public void S03_TV_A015__Char5_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 6, 0);

        [Fact]
        public void S03_TV_A016__Char1_Fallback_Recoil() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 1, 0,
                           new[] { In.B(0), In.B(2304), In.K(2304) },
                           Want.FireViaFallback(ArtId.Recoil), 0, 0);

        [Fact]
        public void S03_TV_A017__Char5_Fire_ShadowHand() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.L(0), In.R(2918), In.K(2304) },
                           Want.Fire(ArtId.ShadowHand), 5, 0);

        [Fact]
        public void S03_TV_A018__Char5_Fire_Wings() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.L(0), In.R(3379), In.K(2304) },
                           Want.Fire(ArtId.Wings), 6, 0);

        [Fact]
        public void S03_TV_A019__Char2_Fire_Fan() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.L(0), In.F(3379), In.K(2304) },
                           Want.Fire(ArtId.Fan), 4, 0);

        [Fact]
        public void S03_TV_A020__Char5_Fallback_Wings() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 0, 1,
                           new[] { In.L(0), In.R(2304), In.K(2304) },
                           Want.FireViaFallback(ArtId.Wings), 0, 2);

        [Fact]
        public void S03_TV_A021__Char1_Fire_Octastar() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2918), In.K(2304) },
                           Want.Fire(ArtId.Octastar), 3, 0);

        [Fact]
        public void S03_TV_A022__Char1_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(3379), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 4, 0);

        [Fact]
        public void S03_TV_A023__Char2_Fire_Octastar() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2918), In.K(2304) },
                           Want.Fire(ArtId.Octastar), 3, 0);

        [Fact]
        public void S03_TV_A024__Char2_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(3379), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 4, 0);

        [Fact]
        public void S03_TV_A025__Char3_Fire_Octastar() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(2918), In.K(2304) },
                           Want.Fire(ArtId.Octastar), 2, 0);

        [Fact]
        public void S03_TV_A026__Char3_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(3379), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 3, 0);

        [Fact]
        public void S03_TV_A027__Char4_Fire_Octastar() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2918), In.K(2304) },
                           Want.Fire(ArtId.Octastar), 4, 0);

        [Fact]
        public void S03_TV_A028__Char4_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(3379), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 5, 0);

        [Fact]
        public void S03_TV_A029__Char5_Fire_Octastar() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.B(2918), In.K(2304) },
                           Want.Fire(ArtId.Octastar), 5, 0);

        [Fact]
        public void S03_TV_A030__Char5_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.B(3379), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 6, 0);

        [Fact]
        public void S03_TV_A031__Char1_Fire_Octastar() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.K(2304) },
                           Want.Fire(ArtId.Octastar), 3, 0);

        [Fact]
        public void S03_TV_A032__Char1_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A033__Char2_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A034__Char3_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A035__Char4_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A036__Char5_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A037__Char1_Fizzle_BloomLate2() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2918), In.B(2611), In.F(2611), In.K(2611) },
                           Want.BloomLate(2, 153, 1), 5, 0);

        [Fact]
        public void S03_TV_A038__Char2_Fizzle_BloomLate2() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2918), In.B(2611), In.F(2611), In.K(2611) },
                           Want.BloomLate(2, 153, 1), 5, 0);

        [Fact]
        public void S03_TV_A039__Char3_Fizzle_BloomLate2() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(2918), In.B(2611), In.F(2611), In.K(2611) },
                           Want.BloomLate(2, 153, 1), 4, 0);

        [Fact]
        public void S03_TV_A040__Char4_Fizzle_BloomLate2() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2918), In.B(2611), In.F(2611), In.K(2611) },
                           Want.BloomLate(2, 153, 1), 6, 0);

        [Fact]
        public void S03_TV_A041__Char5_Fizzle_BloomLate2() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.B(2918), In.B(2611), In.F(2611), In.K(2611) },
                           Want.BloomLate(2, 153, 1), 7, 0);

        [Fact]
        public void S03_TV_A042__Char1_Fizzle_BloomLate5() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(3840) },
                           Want.BloomLate(5, 1075, 7), 5, 0);

        [Fact]
        public void S03_TV_A043__Char2_Fizzle_BloomLate5() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(3840) },
                           Want.BloomLate(5, 1075, 7), 5, 0);

        [Fact]
        public void S03_TV_A044__Char3_Fizzle_BloomLate5() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(3840) },
                           Want.BloomLate(5, 1075, 7), 4, 0);

        [Fact]
        public void S03_TV_A045__Char4_Fizzle_BloomLate5() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(3840) },
                           Want.BloomLate(5, 1075, 7), 6, 0);

        [Fact]
        public void S03_TV_A046__Char5_Fizzle_BloomLate5() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(3840) },
                           Want.BloomLate(5, 1075, 7), 7, 0);

        [Fact]
        public void S03_TV_A047__Char1_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Vent, ArtId.Fan, ArtId.Octastar), 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Vent), 3, 0);

        [Fact]
        public void S03_TV_A048__Char2_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Vent, ArtId.Fan, ArtId.Octastar), 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Vent), 3, 0);

        [Fact]
        public void S03_TV_A049__Char3_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Vent, ArtId.Fan, ArtId.Octastar), 4, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Vent), 2, 0);

        [Fact]
        public void S03_TV_A050__Char4_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Vent, ArtId.Fan, ArtId.Octastar), 6, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Vent), 4, 0);

        [Fact]
        public void S03_TV_A051__Char5_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Vent, ArtId.Fan, ArtId.Octastar), 7, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Vent), 5, 0);

        [Fact]
        public void S03_TV_A052__Char1_Fire_Fan() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Fan), 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Fan), 4, 0);

        [Fact]
        public void S03_TV_A053__Char1_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 2, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.NoCrystal(), 2, 0);

        [Fact]
        public void S03_TV_A054__Char1_Fire_ThunderSpear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.K(922) },
                           Want.Fire(ArtId.ThunderSpear), 4, 0);

        [Fact]
        public void S03_TV_A055__Char1_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.K(1382) },
                           Want.Fire(ArtId.Spear), 3, 0);

        [Fact]
        public void S03_TV_A056__Char1_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.B(0), In.B(3379), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 4, 0);

        [Fact]
        public void S03_TV_A057__Char1_Fire_Kiln() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.B(0), In.B(3072), In.K(2304) },
                           Want.Fire(ArtId.Kiln), 3, 0);

        [Fact]
        public void S03_TV_A058__Char1_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 0, 0,
                           new[] { In.B(0), In.B(2304), In.K(2304) },
                           Want.NoCrystal(), 0, 0);

        [Fact]
        public void S03_TV_A059__Char1_Fire_Kiln() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.B(0), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Kiln), 3, 0);

        [Fact]
        public void S03_TV_A060__Char2_Fire_BoltDash() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.L(0), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.BoltDash), 3, 0);

        [Fact]
        public void S03_TV_A061__Char2_Fire_BoltDash() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.R(0), In.F(3072), In.K(2304) },
                           Want.Fire(ArtId.BoltDash), 3, 0);

        [Fact]
        public void S03_TV_A062__Char2_Fallback_Wings() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 1, 0,
                           new[] { In.L(0), In.F(2304), In.K(2304) },
                           Want.FireViaFallback(ArtId.Wings), 0, 0);

        [Fact]
        public void S03_TV_A063__Char3_Fallback_Fan() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 1, 0,
                           new[] { In.F(0), In.L(2304), In.K(2304) },
                           Want.FireViaFallback(ArtId.Fan), 0, 0);

        [Fact]
        public void S03_TV_A064__Char3_Fire_Wings() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.L(3379), In.K(2304) },
                           Want.Fire(ArtId.Wings), 3, 0);

        [Fact]
        public void S03_TV_A065__Char3_Fire_CopyCircle() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.R(2918), In.K(2304) },
                           Want.Fire(ArtId.CopyCircle), 2, 0);

        [Fact]
        public void S03_TV_A066__Char3_Fire_Fan() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.L(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Fan), 3, 0);

        [Fact]
        public void S03_TV_A067__Char4_Fire_IceScreen() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.B(0), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.IceScreen), 4, 0);

        [Fact]
        public void S03_TV_A068__Char4_Fire_IceScreen() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.B(0), In.L(4454), In.K(2304) },
                           Want.Fire(ArtId.IceScreen), 4, 0);

        [Fact]
        public void S03_TV_A069__Char4_Fire_Wings() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.B(0), In.L(4762), In.K(2304) },
                           Want.Fire(ArtId.Wings), 5, 0);

        [Fact]
        public void S03_TV_A070__Char4_Fallback_Recoil() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 1, 0,
                           new[] { In.B(0), In.R(2304), In.K(2304) },
                           Want.FireViaFallback(ArtId.Recoil), 0, 0);

        [Fact]
        public void S03_TV_A071__Char5_Fire_ShadowHand() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.R(0), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.ShadowHand), 5, 0);

        [Fact]
        public void S03_TV_A072__Char5_Fire_ShadowHand() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 1, 0,
                           new[] { In.L(0), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.ShadowHand), 0, 1);

        [Fact]
        public void S03_TV_A073__Char5_Fire_ShadowHand() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 0, 0,
                           new[] { In.L(0), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.ShadowHand), 0, 2);

        [Fact]
        public void S03_TV_A074__Char5_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 0, 2,
                           new[] { In.L(0), In.R(2304), In.K(2304) },
                           Want.NoCrystal(), 0, 2);

        [Fact]
        public void S03_TV_A075__Char5_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 1, 1,
                           new[] { In.F(0), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 0, 2);

        [Fact]
        public void S03_TV_A076__Char5_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 0, 1,
                           new[] { In.F(0), In.F(2304), In.K(2304) },
                           Want.NoCrystal(), 0, 1);

        [Fact]
        public void S03_TV_A077__Char5_Fire_Burst() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 0, 0,
                           new[] { In.K(0) },
                           Want.Fire(ArtId.Burst), 0, 1);

        [Fact]
        public void S03_TV_A078__Char5_Fire_ShadowHand() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.L(0), In.R(2304), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.ShadowHand), 5, 0);

        [Fact]
        public void S03_TV_A079__Char5_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 3, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A080__Char5_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 2, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.NoCrystal(), 2, 0);

        [Fact]
        public void S03_TV_A081__Char1_Fire_Wings() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.L(0), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.Wings), 4, 0);

        [Fact]
        public void S03_TV_A082__Char3_Fire_Afterimage() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.L(0), In.L(3686), In.K(2304) },
                           Want.Fire(ArtId.Afterimage), 2, 0);

        [Fact]
        public void S03_TV_A083__Char3_Fire_Wings() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.L(0), In.L(3994), In.K(2304) },
                           Want.Fire(ArtId.Wings), 3, 0);

        [Fact]
        public void S03_TV_A084__Char3_Fire_Afterimage() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.L(0), In.L(2304), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.Afterimage), 2, 0);

        [Fact]
        public void S03_TV_A085__Char1_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(3226), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 4, 0);

        [Fact]
        public void S03_TV_A086__Char1_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.B(0), In.F(4454), In.K(2304) },
                           Want.Fire(ArtId.Vent), 3, 0);

        [Fact]
        public void S03_TV_A087__Char3_Fire_Vent() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.F(0), In.B(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Vent), 2, 0);

        [Fact]
        public void S03_TV_A088__Char1_Fire_Fan() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.K(4608) },
                           Want.Fire(ArtId.Fan), 4, 0);

        [Fact]
        public void S03_TV_A089__Char1_Fire_Burst() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.K(4762) },
                           Want.Fire(ArtId.Burst), 4, 0);

        [Fact]
        public void S03_TV_A090__Char1_Fire_Burst() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.K(0) },
                           Want.Fire(ArtId.Burst), 4, 0);

        [Fact]
        public void S03_TV_A091__Char1_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 0, 0,
                           new[] { In.K(0) },
                           Want.NoCrystal(), 0, 0);

        [Fact]
        public void S03_TV_A092__Char1_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 1, 0,
                           new[] { In.F(0), In.F(2304), In.K(2304) },
                           Want.NoCrystal(), 1, 0);

        [Fact]
        public void S03_TV_A093__Char1_Fire_ThunderSpear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 1, 0,
                           new[] { In.F(0), In.F(2304), In.K(768) },
                           Want.Fire(ArtId.ThunderSpear), 0, 0);

        [Fact]
        public void S03_TV_A094__Char1_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 0, 0,
                           new[] { In.F(0), In.F(2304), In.K(768) },
                           Want.NoCrystal(), 0, 0);

        [Fact]
        public void S03_TV_A095__Char1_Fire_ThunderSpear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.K(0) },
                           Want.Fire(ArtId.ThunderSpear), 4, 0);

        [Fact]
        public void S03_TV_A096__Char1_Fire_ThunderSpear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.K(1075) },
                           Want.Fire(ArtId.ThunderSpear), 4, 0);

        [Fact]
        public void S03_TV_A097__Char1_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.K(1080) },
                           Want.Fire(ArtId.Spear), 3, 0);

        [Fact]
        public void S03_TV_A098__Char1_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.Of(ArtId.Spear, ArtId.Burst, ArtId.Recoil, ArtId.Fan), 5, 0,
                           new[] { In.F(0), In.F(2304), In.K(768) },
                           Want.Fire(ArtId.Spear), 3, 0);

        [Fact]
        public void S03_TV_A099__Char1_Fire_ThunderSpear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.F(2304), In.F(2304), In.K(768) },
                           Want.Fire(ArtId.ThunderSpear), 4, 0);

        [Fact]
        public void S03_TV_A100__Char1_Fizzle_BloomLate3() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2918), In.F(2611), In.K(2611) },
                           Want.BloomLate(3, 153, 1), 5, 0);

        [Fact]
        public void S03_TV_A101__Char1_Fizzle_BloomLate4() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(3072), In.K(2611) },
                           Want.BloomLate(4, 307, 2), 5, 0);

        [Fact]
        public void S03_TV_A102__Char1_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2765), In.B(2765), In.F(2765), In.K(2765) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A103__Char1_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Spear), 3, 0);

        [Fact]
        public void S03_TV_A104__Char1_Fizzle_NoCrystal() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 1, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.NoCrystal(), 1, 0);

        [Fact]
        public void S03_TV_A105__Char4_Fire_Bloom() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2611), In.B(2611), In.F(2611), In.K(2611) },
                           Want.Fire(ArtId.Bloom), 0, 0);

        [Fact]
        public void S03_TV_A106__Char3_Fire_Burst() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.Of(ArtId.Burst, ArtId.Recoil), 4, 0,
                           new[] { In.F(0), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Burst), 3, 0);

        [Fact]
        public void S03_TV_A107__Char3_Fire_Fan() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.Of(ArtId.Burst, ArtId.Recoil, ArtId.Fan), 4, 0,
                           new[] { In.F(0), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Fan), 3, 0);

        [Fact]
        public void S03_TV_A108__Char2_Fire_Burst() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.Of(ArtId.Burst, ArtId.Recoil), 5, 0,
                           new[] { In.L(0), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.Burst), 4, 0);

        [Fact]
        public void S03_TV_A109__Char1_Fire_Recoil() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.Of(ArtId.Burst, ArtId.Recoil), 5, 0,
                           new[] { In.F(0), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Recoil), 4, 0);

        [Fact]
        public void S03_TV_A110__Char4_Fire_IceScreen() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(2304), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.IceScreen), 4, 0);

        [Fact]
        public void S03_TV_A111__Char3_Fire_CopyCircle() =>
            ParserRig.RunA(CharacterId.Char3, Unlocks.All, 4, 0,
                           new[] { In.B(0), In.F(2304), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.CopyCircle), 2, 0);

        [Fact]
        public void S03_TV_A112__Char2_Fire_BoltDash() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.L(0), In.L(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.BoltDash), 3, 0);

        [Fact]
        public void S03_TV_A113__Char5_Fire_ShadowHand() =>
            ParserRig.RunA(CharacterId.Char5, Unlocks.All, 7, 0,
                           new[] { In.L(0), In.L(2304), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.ShadowHand), 5, 0);

        [Fact]
        public void S03_TV_A114__Char1_Fire_Kiln() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.F(0), In.B(2304), In.B(2304), In.B(2304), In.K(2304) },
                           Want.Fire(ArtId.Kiln), 3, 0);

        [Fact]
        public void S03_TV_A115__Char2_Fire_Wings() =>
            ParserRig.RunA(CharacterId.Char2, Unlocks.All, 5, 0,
                           new[] { In.L(0), In.R(2304), In.K(2304) },
                           Want.Fire(ArtId.Wings), 4, 0);

        [Fact]
        public void S03_TV_A116__Char4_Fire_IceScreen() =>
            ParserRig.RunA(CharacterId.Char4, Unlocks.All, 6, 0,
                           new[] { In.F(0), In.B(3840), In.L(2304), In.K(2304) },
                           Want.Fire(ArtId.IceScreen), 4, 0);

        [Fact]
        public void S03_TV_A117__Char1_Fire_Spear() =>
            ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0,
                           new[] { In.L(0), In.R(2304), In.L(2304), In.R(2304), In.L(2304), In.R(2304), In.L(2304), In.R(2304), In.F(2304), In.F(2304), In.K(2304) },
                           Want.Fire(ArtId.Spear), 3, 0);
    }
}
