// Copyright (c) 2025 PPN Corporation. All rights reserved.

using Nalix.Common.Attributes;

namespace Nalix.Common.Packets.Enums;

/// <summary>
/// Represents the positions of fields in the serialization order.
/// Each value corresponds to a specific position in the serialized data.
/// </summary>
public enum PacketHeaderOffset : System.Byte
{
    /// <summary>
    /// Represents the magic number field, which uniquely identifies the packet format or protocol.
    /// This field comes first in the serialized data.
    /// </summary>
    [DataType(typeof(System.UInt32))]
    MagicNumber = 0,

    /// <summary>
    /// Represents the operation code (OpCode) field, specifying the command or type of the packet.
    /// This field comes second in the serialized data.
    /// </summary>
    [DataType(typeof(System.UInt16))]
    OpCode = MagicNumber + sizeof(System.UInt32),

    /// <summary>
    /// Represents the flags field, which contains state or processing options for the packet.
    /// This field comes third in the serialized data.
    /// </summary>
    [DataType(typeof(System.Byte))]
    Flags = OpCode + sizeof(System.UInt16),

    /// <summary>
    /// Represents the priority field, indicating the processing priority of the packet.
    /// This field comes fourth in the serialized data.
    /// </summary>
    [DataType(typeof(System.Byte))]
    Priority = Flags + sizeof(System.Byte),

    /// <summary>
    /// Represents the transport protocol field, indicating the transport protocol (e.g., TCP or UDP) used.
    /// This field comes fifth in the serialized data.
    /// </summary>
    [DataType(typeof(System.Byte))]
    Transport = Priority + sizeof(System.Byte),

    /// <summary>
    /// Represents the end offset of the packet header fields in the serialized data.
    /// This value is equal to the offset of the last field and can be used to determine the total header size.
    /// </summary>
    DataRegion = Transport + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_1 = DataRegion + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_2 = Data_1 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_3 = Data_2 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_4 = Data_3 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_5 = Data_4 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_6 = Data_5 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_7 = Data_6 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_8 = Data_7 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_9 = Data_8 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_10 = Data_9 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_11 = Data_10 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_12 = Data_11 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_13 = Data_12 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_14 = Data_13 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_15 = Data_14 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_16 = Data_15 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_17 = Data_16 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_18 = Data_17 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_19 = Data_18 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_20 = Data_19 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_21 = Data_20 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_22 = Data_21 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_23 = Data_22 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_24 = Data_23 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_25 = Data_24 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_26 = Data_25 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_27 = Data_26 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_28 = Data_27 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_29 = Data_28 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_30 = Data_29 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_31 = Data_30 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_32 = Data_31 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_33 = Data_32 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_34 = Data_33 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_35 = Data_34 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_36 = Data_35 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_37 = Data_36 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_38 = Data_37 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_39 = Data_38 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_40 = Data_39 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_41 = Data_40 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_42 = Data_41 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_43 = Data_42 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_44 = Data_43 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_45 = Data_44 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_46 = Data_45 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_47 = Data_46 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_48 = Data_47 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_49 = Data_48 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_50 = Data_49 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_51 = Data_50 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_52 = Data_51 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_53 = Data_52 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_54 = Data_53 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_55 = Data_54 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_56 = Data_55 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_57 = Data_56 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_58 = Data_57 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_59 = Data_58 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_60 = Data_59 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_61 = Data_60 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_62 = Data_61 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_63 = Data_62 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_64 = Data_63 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_65 = Data_64 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_66 = Data_65 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_67 = Data_66 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_68 = Data_67 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_69 = Data_68 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_70 = Data_69 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_71 = Data_70 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_72 = Data_71 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_73 = Data_72 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_74 = Data_73 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_75 = Data_74 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_76 = Data_75 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_77 = Data_76 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_78 = Data_77 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_79 = Data_78 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_80 = Data_79 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_81 = Data_80 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_82 = Data_81 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_83 = Data_82 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_84 = Data_83 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_85 = Data_84 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_86 = Data_85 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_87 = Data_86 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_88 = Data_87 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_89 = Data_88 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_90 = Data_89 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_91 = Data_90 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_92 = Data_91 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_93 = Data_92 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_94 = Data_93 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_95 = Data_94 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_96 = Data_95 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_97 = Data_96 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_98 = Data_97 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_99 = Data_98 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_100 = Data_99 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_101 = Data_100 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_102 = Data_101 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_103 = Data_102 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_104 = Data_103 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_105 = Data_104 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_106 = Data_105 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_107 = Data_106 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_108 = Data_107 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_109 = Data_108 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_110 = Data_109 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_111 = Data_110 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_112 = Data_111 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_113 = Data_112 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_114 = Data_113 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_115 = Data_114 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_116 = Data_115 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_117 = Data_116 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_118 = Data_117 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_119 = Data_118 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_120 = Data_119 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_121 = Data_120 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_122 = Data_121 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_123 = Data_122 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_124 = Data_123 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_125 = Data_124 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_126 = Data_125 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_127 = Data_126 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_128 = Data_127 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_129 = Data_128 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_130 = Data_129 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_131 = Data_130 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_132 = Data_131 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_133 = Data_132 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_134 = Data_133 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_135 = Data_134 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_136 = Data_135 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_137 = Data_136 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_138 = Data_137 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_139 = Data_138 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_140 = Data_139 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_141 = Data_140 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_142 = Data_141 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_143 = Data_142 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_144 = Data_143 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_145 = Data_144 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_146 = Data_145 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_147 = Data_146 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_148 = Data_147 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_149 = Data_148 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_150 = Data_149 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_151 = Data_150 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_152 = Data_151 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_153 = Data_152 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_154 = Data_153 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_155 = Data_154 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_156 = Data_155 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_157 = Data_156 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_158 = Data_157 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_159 = Data_158 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_160 = Data_159 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_161 = Data_160 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_162 = Data_161 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_163 = Data_162 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_164 = Data_163 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_165 = Data_164 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_166 = Data_165 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_167 = Data_166 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_168 = Data_167 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_169 = Data_168 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_170 = Data_169 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_171 = Data_170 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_172 = Data_171 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_173 = Data_172 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_174 = Data_173 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_175 = Data_174 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_176 = Data_175 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_177 = Data_176 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_178 = Data_177 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_179 = Data_178 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_180 = Data_179 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_181 = Data_180 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_182 = Data_181 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_183 = Data_182 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_184 = Data_183 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_185 = Data_184 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_186 = Data_185 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_187 = Data_186 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_188 = Data_187 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_189 = Data_188 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_190 = Data_189 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_191 = Data_190 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_192 = Data_191 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_193 = Data_192 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_194 = Data_193 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_195 = Data_194 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_196 = Data_195 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_197 = Data_196 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_198 = Data_197 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_199 = Data_198 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_200 = Data_199 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_201 = Data_200 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_202 = Data_201 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_203 = Data_202 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_204 = Data_203 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_205 = Data_204 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_206 = Data_205 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_207 = Data_206 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_208 = Data_207 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_209 = Data_208 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_210 = Data_209 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_211 = Data_210 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_212 = Data_211 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_213 = Data_212 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_214 = Data_213 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_215 = Data_214 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_216 = Data_215 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_217 = Data_216 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_218 = Data_217 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_219 = Data_218 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_220 = Data_219 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_221 = Data_220 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_222 = Data_221 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_223 = Data_222 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_224 = Data_223 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_225 = Data_224 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_226 = Data_225 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_227 = Data_226 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_228 = Data_227 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_229 = Data_228 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_230 = Data_229 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_231 = Data_230 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_232 = Data_231 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_233 = Data_232 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_234 = Data_233 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_235 = Data_234 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_236 = Data_235 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_237 = Data_236 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_238 = Data_237 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_239 = Data_238 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_240 = Data_239 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_241 = Data_240 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_242 = Data_241 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_243 = Data_242 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_244 = Data_243 + sizeof(System.Byte),

    /// <inheritdoc/>
    Data_245 = Data_244 + sizeof(System.Byte),

    /// <inheritdoc/>
    MaxValue = System.Byte.MaxValue,
}
