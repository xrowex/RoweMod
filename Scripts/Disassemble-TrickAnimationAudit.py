"""Read-only x64 audit. Requires capstone + pefile; output is analysis, never a patch."""
import argparse, json, bisect, re, struct
from pathlib import Path
import capstone, pefile

p = argparse.ArgumentParser()
p.add_argument('binary'); p.add_argument('metadata'); p.add_argument('output')
args = p.parse_args()
data = json.loads(Path(args.metadata).read_text(encoding='utf-8-sig'))
methods = {int(k, 16): v for k, v in data['methods'].items()}
starts = sorted(methods)
pe = pefile.PE(args.binary, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
cs = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
def names(values):
    relevant = [n for n in values if 'Trick' in n or n.startswith(('System.Void Animancer.', 'System.Single Animancer.', 'System.Boolean UnityEngine.Object'))]
    return ' / '.join((relevant or values)[:4]) + (f' [shared: {len(values)} aliases]' if len(values) > 1 else '')
lines = ['SHA256 ' + data['hash']]
for t in data['types']:
    lines.append('\n' + t['name'])
    lines += [f"{f['offset']} {f['type']} {f['name']}" for f in t['fields']]
for rva in starts:
    if not any(('TrickControllerV2::' in n or 'TrickAnimator::' in n or 'SyncTrickAnimationData::' in n or 'TrickSystemBrainV2::' in n) for n in methods[rva]):
        continue
    end = starts[bisect.bisect_right(starts, rva)]
    lines.append('\n' + hex(rva) + ' ' + names(methods[rva]))
    for ins in cs.disasm(pe.get_data(rva, min(end-rva, 16384)), base+rva):
        extra = ''
        if ins.mnemonic in ('call', 'jmp') and ins.op_str.startswith('0x'):
            target = int(ins.op_str, 16)-base
            extra = ' ; ' + names(methods.get(target, []))
        if ins.mnemonic in ('movss', 'comiss', 'ucomiss', 'mulss', 'maxss', 'minss'):
            match = re.search(r'\[rip ([+-]) (0x[0-9a-f]+)\]', ins.op_str)
            if match:
                target = ins.address-base+ins.size+int(match[2],16)*(1 if match[1]=='+' else -1)
                extra += f' ; float={struct.unpack("<f", pe.get_data(target,4))[0]:.7g}'
        lines.append(f'{ins.address-base:08x} {ins.mnemonic:8} {ins.op_str}{extra}')
Path(args.output).write_text('\n'.join(lines), encoding='utf-8')
print('Saved native animation disassembly to', args.output)
