// ==Highlighted== text, the extension Markdig reads as <mark> in the read-only rendering. Parsed and written like GFM strikethrough (micromark-extension-gfm-strikethrough),
// with == in place of ~~, so the formatted editor reads and writes it as Markdig does.
import { toggleMark } from '@milkdown/kit/prose/commands';
import { markRule } from '@milkdown/kit/prose';
import { $command, $inputRule, $markSchema, $remark } from '@milkdown/kit/utils';
import { splice } from 'micromark-util-chunked';
import { classifyCharacter } from 'micromark-util-classify-character';
import { resolveAll } from 'micromark-util-resolve-all';

const equalsSign = 61;
const sequenceLength = 2;
const marker = '=';
const sequence = marker.repeat(sequenceLength);
const punctuation = 2;
const markNodeType = 'mark';
const constructName = 'highlight';
const temporarySequence = 'highlightSequenceTemporary';

// Phrasing constructs that cannot contain a highlight, as for strikethrough in mdast-util-gfm-strikethrough.
const constructsWithoutHighlight = ['autolink', 'destinationLiteral', 'destinationRaw', 'reference', 'titleQuote', 'titleApostrophe'];

function resolveAllHighlight(events, context) {
    let index = -1;
    while (++index < events.length) {
        if (events[index][0] !== 'enter' || events[index][1].type !== temporarySequence || !events[index][1]._close) {
            continue;
        }
        let open = index;
        while (open--) {
            if (events[open][0] === 'exit' && events[open][1].type === temporarySequence && events[open][1]._open) {
                events[index][1].type = 'highlightSequence';
                events[open][1].type = 'highlightSequence';
                const highlight = { type: constructName, start: { ...events[open][1].start }, end: { ...events[index][1].end } };
                const text = { type: 'highlightText', start: { ...events[open][1].end }, end: { ...events[index][1].start } };
                const nextEvents = [['enter', highlight, context], ['enter', events[open][1], context], ['exit', events[open][1], context], ['enter', text, context]];
                const insideSpan = context.parser.constructs.insideSpan.null;
                if (insideSpan) {
                    splice(nextEvents, nextEvents.length, 0, resolveAll(insideSpan, events.slice(open + 1, index), context));
                }
                splice(nextEvents, nextEvents.length, 0, [['exit', text, context], ['enter', events[index][1], context], ['exit', events[index][1], context], ['exit', highlight, context]]);
                splice(events, open - 1, index - open + 3, nextEvents);
                index = open + nextEvents.length - 2;
                break;
            }
        }
    }
    events.forEach(([, token]) => {
        if (token.type === temporarySequence) {
            token.type = 'data';
        }
    });
    return events;
}

function tokenizeHighlight(effects, ok, nok) {
    const previous = this.previous;
    const events = this.events;
    let size = 0;

    const more = (code) => {
        const before = classifyCharacter(previous);
        if (code === equalsSign) {
            if (size === sequenceLength) {
                return nok(code);
            }
            effects.consume(code);
            size++;
            return more;
        }
        if (size < sequenceLength) {
            return nok(code);
        }
        const token = effects.exit(temporarySequence);
        const after = classifyCharacter(code);
        token._open = !after || (after === punctuation && Boolean(before));
        token._close = !before || (before === punctuation && Boolean(after));
        return ok(code);
    };

    return (code) => {
        if (previous === equalsSign && events[events.length - 1][1].type !== 'characterEscape') {
            return nok(code);
        }
        effects.enter(temporarySequence);
        return more(code);
    };
}

const tokenizer = { name: constructName, tokenize: tokenizeHighlight, resolveAll: resolveAllHighlight };
const micromarkHighlight = { text: { [equalsSign]: tokenizer }, insideSpan: { null: [tokenizer] }, attentionMarkers: { null: [equalsSign] } };

const fromMarkdown = {
    canContainEols: [markNodeType],
    enter: { [constructName](token) { this.enter({ type: markNodeType, children: [] }, token); } },
    exit: { [constructName](token) { this.exit(token); } }
};

function handleMark(node, _, state, info) {
    const tracker = state.createTracker(info);
    const exit = state.enter(constructName);
    let value = tracker.move(sequence);
    value += state.containerPhrasing(node, { ...tracker.current(), before: value, after: marker });
    value += tracker.move(sequence);
    exit();
    return value;
}
handleMark.attention = () => ({ construct: constructName, markers: [marker], sizes: [sequenceLength] });
handleMark.peek = () => marker;

// An = is escaped only where it starts a pair that could open (text follows) or close (text precedes) a highlight, so "x = 5" and "a == b" are written as typed.
// The lookaheads keep each match to one character, so every = of a longer run is checked.
const unsafeIn = { inConstruct: 'phrasing', notInConstruct: constructsWithoutHighlight };
const toMarkdown = {
    handlers: { [markNodeType]: handleMark },
    unsafe: [
        { character: marker, after: `(?=${marker}\\S)`, ...unsafeIn },
        { character: marker, before: '\\S', after: `(?=${marker})`, ...unsafeIn }
    ]
};

const remarkHighlight = $remark('remarkHighlight', () => function () {
    const data = this.data();
    (data.micromarkExtensions ??= []).push(micromarkHighlight);
    (data.fromMarkdownExtensions ??= []).push(fromMarkdown);
    (data.toMarkdownExtensions ??= []).push(toMarkdown);
});

export const highlightSchema = $markSchema(constructName, () => ({
    parseDOM: [{ tag: markNodeType }],
    toDOM: () => [markNodeType, 0],
    parseMarkdown: {
        match: (node) => node.type === markNodeType,
        runner: (state, node, markType) => {
            state.openMark(markType);
            state.next(node.children);
            state.closeMark(markType);
        }
    },
    toMarkdown: {
        match: (mark) => mark.type.name === constructName,
        runner: (state, mark) => {
            state.withMark(mark, markNodeType);
        }
    }
}));

export const toggleHighlightCommand = $command('ToggleHighlight', (ctx) => () => toggleMark(highlightSchema.type(ctx)));

// Typing ==text== highlights it as it is typed, as ~~text~~ strikes it through.
const highlightInputRule = $inputRule((ctx) => markRule(/(?<![\w=])(==)([^=]+?)==$/, highlightSchema.type(ctx)));

export const highlight = [remarkHighlight, highlightSchema, toggleHighlightCommand, highlightInputRule].flat();
