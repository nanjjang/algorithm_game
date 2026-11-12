using System;

namespace Wizard.Text
{
    // spec 14 2.4절 FinalSound: 끝 글자의 받침 유무와 ㄹ 받침 여부로 조사를 고른다.
    //   한글 음절(U+AC00~U+D7A3): 종성 = (c − 0xAC00) mod 28. 0 이면 받침 없음, 8 이면 ㄹ.
    //   숫자: 읽은 소리로 본다(WC 0: 1 일, 2 이, 3 삼, 4 사, 5 오. 0·6~9 는 [임시] 영·육·칠·팔·구, 9절 U-06).
    //         여러 자리 수도 끝 자리만 본다("10" 을 "십" 으로 읽지 않는다) [임시].
    //   그 밖(영문, 기호, 낱자모): 받침 없음 [임시: U-06].
    //   끝의 공백, ')', ']', '"', '…' 는 건너뛴다 [임시]. 건너뛰고 남는 글자가 없으면 받침 없음.
    public static class Josa
    {
        private const int SyllableFirst = 0xAC00;   // 유니코드 한글 음절 블록의 첫 글자 '가'
        private const int SyllableLast = 0xD7A3;    // 마지막 글자 '힣'
        private const int FinalCount = 28;          // 종성 수(받침 없음 포함)
        private const int RieulFinal = 8;           // 종성 번호 8 = ㄹ

        // 숫자 0~9 를 읽은 소리(영 일 이 삼 사 오 육 칠 팔 구)의 받침 / ㄹ 받침
        private static readonly bool[] DigitHasFinal = { true, true, false, true, false, false, true, true, true, false };
        private static readonly bool[] DigitIsRieul = { false, true, false, false, false, false, false, true, true, false };

        public static void FinalSound(string s, out bool hasFinal, out bool isRieul)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }
            int i = s.Length - 1;
            while (i >= 0 && IsSkipped(s[i]))
            {
                i--;
            }
            hasFinal = false;
            isRieul = false;
            if (i < 0)
            {
                return;
            }
            char c = s[i];
            if (c >= SyllableFirst && c <= SyllableLast)
            {
                int final = (c - SyllableFirst) % FinalCount;
                hasFinal = final != 0;
                isRieul = final == RieulFinal;
                return;
            }
            if (c >= '0' && c <= '9')
            {
                hasFinal = DigitHasFinal[c - '0'];
                isRieul = DigitIsRieul[c - '0'];
            }
        }

        private static bool IsSkipped(char c) => c == ' ' || c == ')' || c == ']' || c == '"' || c == '…';
    }
}
