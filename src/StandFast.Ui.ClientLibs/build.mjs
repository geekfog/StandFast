// Bundles the browser libraries into StandFast.Ui's wwwroot. The output is committed, so only upgrading a library needs Node.js.
import { build } from 'esbuild';

const outdir = '../StandFast.Ui/wwwroot/lib/milkdown';

await build({
    entryPoints: { milkdown: 'milkdown.entry.js' },
    outdir,
    bundle: true,
    format: 'esm',
    minify: true,
    legalComments: 'linked',
    target: 'es2022',
    logLevel: 'info'
});
