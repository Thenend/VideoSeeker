namespace VideoSeeker.Inspect;

internal static class FrameShaders
{
    // Triángulo que cubre todo el viewport; el píxel convierte YUV a RGB con la matriz que calcula la CPU.
    // En HDR (PQ/HLG) se pasa a lineal, de BT.2020 a BT.709, y se comprime a SDR con un Reinhard extendido.
    public const string Source = """
        Texture2D<float>  texLuma   : register(t0);
        Texture2D<float2> texChroma : register(t1);
        Texture2D<float4> texRgb    : register(t2);
        SamplerState samp : register(s0);

        cbuffer Params : register(b0)
        {
            float4 rowR;
            float4 rowG;
            float4 rowB;
            int layout;     // 0 = luma + croma, 1 = RGB
            int transfer;   // 0 = SDR, 1 = PQ, 2 = HLG
            float peak;     // pico del HDR relativo al blanco SDR
            float pad;
        };

        struct VSOut { float4 pos : SV_Position; float2 uv : TEXCOORD0; };

        VSOut VS(uint id : SV_VertexID)
        {
            VSOut o;
            float2 uv = float2((id << 1) & 2, id & 2);
            o.uv = uv;
            o.pos = float4(uv * float2(2, -2) + float2(-1, 1), 0, 1);
            return o;
        }

        float3 PqToNits(float3 e)
        {
            const float m1 = 0.1593017578125, m2 = 78.84375, c1 = 0.8359375, c2 = 18.8515625, c3 = 18.6875;
            float3 p = pow(max(e, 0), 1 / m2);
            return 10000 * pow(max(p - c1, 0) / (c2 - c3 * p), 1 / m1);
        }

        float3 HlgToNits(float3 e)
        {
            const float a = 0.17883277, b = 0.28466892, c = 0.55991073;
            float3 lin = e <= 0.5 ? e * e / 3 : (exp((e - c) / a) + b) / 12;
            return lin * 1000;
        }

        float4 PS(VSOut i) : SV_Target
        {
            if (layout == 1)
                return float4(texRgb.Sample(samp, i.uv).rgb, 1);

            float4 ycc = float4(texLuma.Sample(samp, i.uv), texChroma.Sample(samp, i.uv), 1);
            float3 rgb = float3(dot(rowR, ycc), dot(rowG, ycc), dot(rowB, ycc));
            if (transfer == 0)
                return float4(saturate(rgb), 1);

            float3 nits = transfer == 1 ? PqToNits(saturate(rgb)) : HlgToNits(saturate(rgb));
            float3 lin2020 = nits / 203;
            float3 lin709 = float3(
                dot(float3( 1.6605, -0.5876, -0.0728), lin2020),
                dot(float3(-0.1246,  1.1329, -0.0083), lin2020),
                dot(float3(-0.0182, -0.1006,  1.1187), lin2020));
            float w2 = peak * peak;
            float3 mapped = lin709 * (1 + lin709 / w2) / (1 + lin709);
            return float4(pow(saturate(mapped), 1 / 2.2), 1);
        }
        """;
}
