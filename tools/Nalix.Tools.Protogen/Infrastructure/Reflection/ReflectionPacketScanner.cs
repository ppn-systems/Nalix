using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nalix.Tools.Protogen.Domain.Interfaces;
using Nalix.Tools.Protogen.Domain.Models;

namespace Nalix.Tools.Protogen.Infrastructure.Reflection;

public class ReflectionPacketScanner : IPacketScanner
{
    private static readonly NullabilityInfoContext _nullabilityContext = new();

    public List<PacketDefinition> Scan(IEnumerable<string> inputPaths)
    {
        var loadPaths = new HashSet<string>();
        foreach (var path in inputPaths)
        {
            if (Directory.Exists(path))
            {
                loadPaths.Add(path);
            }
            else if (File.Exists(path))
            {
                loadPaths.Add(Path.GetDirectoryName(Path.GetFullPath(path))!);
            }
        }

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var name = new AssemblyName(args.Name).Name;
            foreach (var dir in loadPaths)
            {
                string p = Path.Combine(dir, name + ".dll");
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };

        var assemblies = new List<Assembly>();
        foreach (var path in inputPaths)
        {
            if (File.Exists(path) && path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblies.Add(Assembly.LoadFrom(Path.GetFullPath(path)));
            }
            else if (Directory.Exists(path))
            {
                foreach (var dll in Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    assemblies.Add(Assembly.LoadFrom(Path.GetFullPath(dll)));
                }
            }
            else
            {
                throw new FileNotFoundException($"Input path '{path}' not found.");
            }
        }

        var packets = new List<PacketDefinition>();

        foreach (var assembly in assemblies)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"[WARNING] Could not load some types from {assembly.GetName().Name}: {ex.Message}");
                types = ex.Types.Where(t => t != null).ToArray()!;
            }

            foreach (var type in types)
            {
                if (IsPacket(type))
                {
                    var packet = AnalyzeType(type);
                    packets.Add(packet);
                }
            }
        }

        return packets;
    }

    private PacketDefinition AnalyzeType(Type type)
    {
        var packet = new PacketDefinition
        {
            Name = type.Name,
            Namespace = type.Namespace ?? string.Empty
        };

        // Determine Magic Number
        var magicProp = type.GetProperty("MagicNumber", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (magicProp != null)
        {
            try
            {
                object? instance = magicProp.GetMethod!.IsStatic ? null : Activator.CreateInstance(type);
                packet.MagicNumber = Convert.ToUInt32(magicProp.GetValue(instance));
            }
            catch { packet.MagicNumber = ComputeMagic(type.FullName ?? type.Name); }
        }
        else
        {
            packet.MagicNumber = ComputeMagic(type.FullName ?? type.Name);
        }

        // Determine OpCode
        var opCodeProp = type.GetProperty("OpCode", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
        if (opCodeProp != null)
        {
            try
            {
                object? instance = opCodeProp.GetMethod!.IsStatic ? null : Activator.CreateInstance(type);
                packet.OpCode = Convert.ToUInt16(opCodeProp.GetValue(instance));
            }
            catch { /* ignore */ }
        }

        // Get Serializable Properties
        var serializableProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new
            {
                Prop = p,
                Attr = p.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "SerializeOrderAttribute")
            })
            .Where(x => x.Attr != null)
            .Select(x => new
            {
                x.Prop,
                Order = Convert.ToInt32(x.Attr!.ConstructorArguments[0].Value)
            })
            .OrderBy(x => x.Order)
            .ToList();

        foreach (var item in serializableProps)
        {
            var propInfo = item.Prop;
            var nullability = _nullabilityContext.Create(propInfo);

            var propDef = new PropertyDefinition
            {
                Name = propInfo.Name,
                Order = item.Order,
                IsNullable = nullability.ReadState == NullabilityState.Nullable
            };

            ReflectionTypeMapper.MapType(propInfo.PropertyType, propDef);
            
            if (propDef.Kind == TypeKind.Unknown)
            {
                if (IsPacket(propInfo.PropertyType))
                {
                    propDef.Kind = TypeKind.NestedPacket;
                }
                else
                {
                    Console.WriteLine($"[WARNING] Unmapped complex type '{propInfo.PropertyType.Name}' for property '{propDef.Name}'. Treated as Unknown.");
                }
            }
            
            packet.Properties.Add(propDef);
        }

        return packet;
    }

    private static bool IsPacket(Type type)
    {
        if (type.IsAbstract || type.IsInterface) return false;
        if (!type.IsClass && !type.IsValueType) return false;

        if (type.CustomAttributes.Any(a => a.AttributeType.Name == "SerializePackableAttribute"))
            return true;

        Type? current = type.BaseType;
        while (current != null)
        {
            if (current.Name.StartsWith("PacketBase"))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static uint ComputeMagic(string fullName)
    {
        const uint FNV_OFFSET_BASIS = 2166136261;
        const uint FNV_PRIME = 16777619;
        uint hash = FNV_OFFSET_BASIS;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(fullName);
        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= FNV_PRIME;
        }
        return hash;
    }
}
