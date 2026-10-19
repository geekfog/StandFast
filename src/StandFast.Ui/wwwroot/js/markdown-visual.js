// Formatted (visual) markdown editing on Milkdown. Blazor owns the text: the editor reports every change it makes, and Blazor pushes back only text it changed itself.
import * as Milkdown from '../lib/milkdown/milkdown.js';

const editors = new Map();

// The read-only rendering (MarkdownRenderer) shows every line break and every blank line as typed, so the visual editor parses and writes markdown the same way:
// a line break is a line break, each blank line is one empty paragraph, and consecutive one-line blocks sit on adjacent source lines.
const containerTypes = new Set(['list', 'blockquote', 'table']);
const spacedParentTypes = new Set(['root', 'blockquote']);
const firstListNumber = 1;
const lineBreakPattern = /[\t ]*\r?\n/;
const emptyLinePlaceholderPattern = /^<br\s*\/?\s*>$/i;
const bareUrlPattern = /^(?:https?:\/\/|mailto:)\S+$/i;
const trailingWhitespacePattern = /\s+$/;
const nodeTypes = { blockquote: 'blockquote', bulletList: 'bullet_list', heading: 'heading', listItem: 'list_item', orderedList: 'ordered_list' };

const isEmptyLine = (node) => node.type === 'paragraph'
    && (node.children.length === 0 || (node.children.length === 1 && node.children[0].type === 'html' && emptyLinePlaceholderPattern.test(node.children[0].value.trim())));

// Whether markdown needs a blank line between two blocks to keep them apart: lists, quotes and tables absorb the line after them, and a table or a list numbered from
// anything but 1 cannot start straight after a paragraph. Such a blank line is structural, so the editor shows it as spacing rather than as an empty paragraph.
const needsSeparator = (left, right) => !isEmptyLine(left)
    && (containerTypes.has(left.type) || right.type === 'table' || (right.type === 'list' && right.ordered && (right.start ?? firstListNumber) !== firstListNumber));

const emptyParagraph = () => ({ type: 'paragraph', children: [] });

const splitLines = (text) => text.value.split(lineBreakPattern).flatMap((value, index) => [...(index === 0 ? [] : [{ type: 'break' }]), ...(value === '' ? [] : [{ type: 'text', value }])]);

// One paragraph per line, so block formatting such as a heading or a list applies to the line it is on, as it does in the markdown source.
const paragraphPerLine = (paragraph) => paragraph.children.reduce((lines, child) => {
    if (child.type === 'break') {
        lines.push(emptyParagraph());
    } else {
        lines.at(-1).children.push(child);
    }
    return lines;
}, [emptyParagraph()]);

// Parsing: every line break becomes a hard break, which is a new paragraph outside lists, and every blank line that is not structural becomes an empty paragraph,
// counted from the source positions.
const lineModelPlugin = () => (tree) => {
    const visit = (node) => {
        if (node.type === 'break' && node.data) {
            delete node.data.isInline;
        }
        if (!node.children) {
            return;
        }
        node.children = node.children.flatMap((child) => child.type === 'text' && lineBreakPattern.test(child.value) ? splitLines(child) : [child]);
        node.children.forEach(visit);
        if (!spacedParentTypes.has(node.type)) {
            return;
        }
        node.children = node.children.flatMap((child, index) => {
            const previous = node.children[index - 1];
            const blankLines = previous?.position && child.position ? child.position.start.line - previous.position.end.line - 1 : 0;
            const emptyLines = Math.max(0, previous && needsSeparator(previous, child) ? blankLines - 1 : blankLines);
            return [...Array.from({ length: emptyLines }, emptyParagraph), ...(child.type === 'paragraph' ? paragraphPerLine(child) : [child])];
        });
    };
    visit(tree);
};

// Writing: the reverse of lineModelPlugin. 0 puts the next block on the following line; undefined leaves the blank line markdown would otherwise use.
const joinBlocks = (left, right, parent) => spacedParentTypes.has(parent.type) && !needsSeparator(left, right) ? 0 : undefined;

const isBareUrl = (node) => !node.title && node.children.length === 1 && node.children[0].type === 'text' && node.children[0].value === node.url && bareUrlPattern.test(node.url);

const configure = (entry, italicMarker) => (ctx) => {
    ctx.update(Milkdown.remarkPluginsCtx, (plugins) => [...plugins, { plugin: lineModelPlugin, options: {} }]);
    ctx.update(Milkdown.remarkStringifyOptionsCtx, (options) => ({
        ...options,
        bullet: '-',
        emphasis: italicMarker,
        rule: '*',
        listItemIndent: 'one',
        join: [...(options.join ?? []), joinBlocks],
        handlers: {
            ...options.handlers,
            break: () => '\n',
            html: (node) => emptyLinePlaceholderPattern.test(node.value.trim()) ? '' : node.value,
            // A URL typed as plain text stays plain text, which the read-only rendering links on its own.
            link: (node, parent, state, info) => isBareUrl(node) ? node.url : Milkdown.defaultHandlers.link(node, parent, state, info)
        }
    }));
    ctx.update(Milkdown.editorViewOptionsCtx, (options) => ({ ...options, attributes: { class: 'standfast-markdown' } }));
    // Pasted or dropped images would only ever link to this browser's memory, so they are ignored.
    ctx.update(Milkdown.uploadConfig.key, (options) => ({ ...options, enableHtmlFileUploader: false, uploader: async () => [] }));
    ctx.update(Milkdown.prosePluginsCtx, (plugins) => [...plugins, changePlugin(entry)]);
};

const markdownOf = (entry, doc) => {
    let markdown = entry.markdownByDoc.get(doc);
    if (markdown === undefined) {
        markdown = entry.serialize(doc).replace(trailingWhitespacePattern, '');
        entry.markdownByDoc.set(doc, markdown);
    }
    return markdown;
};

// Refuses an edit that would take the text past its length limit, and reports every other edit to Blazor as it happens.
const changePlugin = (entry) => new Milkdown.Plugin({
    filterTransaction: (transaction) => !transaction.docChanged || entry.applying || !entry.serialize || markdownOf(entry, transaction.doc).length <= entry.maxLength,
    view: () => ({
        update: (view, previousState) => {
            if (entry.applying || !entry.serialize || view.state.doc.eq(previousState.doc)) {
                return;
            }
            const markdown = markdownOf(entry, view.state.doc);
            if (markdown !== entry.markdown) {
                entry.markdown = markdown;
                entry.dotNetReference.invokeMethodAsync(entry.changedMethodName, markdown);
            }
        }
    })
});

const isInside = (view, typeName) => {
    const { $from } = view.state.selection;
    for (let depth = $from.depth; depth > 0; depth--) {
        if ($from.node(depth).type.name === typeName) {
            return true;
        }
    }
    return false;
};

// The innermost list item of each line in the selection, by position.
const selectedListItems = (state) => {
    const items = new Map();
    const { from, to } = state.selection;
    state.doc.nodesBetween(from, to, (node, position) => {
        if (!node.isTextblock) {
            return true;
        }
        const $position = state.doc.resolve(position + 1);
        const item = $position.depth > 1 ? $position.node($position.depth - 1) : null;
        if (item?.type.name === nodeTypes.listItem) {
            items.set($position.before($position.depth - 1), item);
        }
        return false;
    });
    return items;
};

const setTaskState = (view, checked) => {
    const transaction = view.state.tr;
    selectedListItems(view.state).forEach((item, position) => transaction.setNodeMarkup(position, undefined, { ...item.attrs, checked }));
    view.dispatch(transaction);
};

const run = (editor, command, payload) => editor.action(Milkdown.callCommand(command.key, payload));

const callCommand = (command) => (editor) => run(editor, command);

// A list button takes lines out of a list of its own kind, and otherwise moves them into one.
const toggleList = (editor, view, listType, wrapCommand) => {
    const otherListType = listType === nodeTypes.bulletList ? nodeTypes.orderedList : nodeTypes.bulletList;
    if (isInside(view, listType)) {
        return run(editor, Milkdown.liftListItemCommand);
    }
    while (isInside(view, otherListType) && run(editor, Milkdown.liftListItemCommand)) {
        // Lifts one level per pass until the lines are out of the other kind of list.
    }
    return run(editor, wrapCommand);
};

const toggleBulletList = (editor, view) => toggleList(editor, view, nodeTypes.bulletList, Milkdown.wrapInBulletListCommand);

const isTask = (view) => [...selectedListItems(view.state).values()].some((item) => item.attrs.checked !== null);

// Keyed by MarkdownVisualAction in MarkdownCommands.cs.
const actions = {
    Bold: callCommand(Milkdown.toggleStrongCommand),
    Italic: callCommand(Milkdown.toggleEmphasisCommand),
    Strikethrough: callCommand(Milkdown.toggleStrikethroughCommand),
    Highlight: callCommand(Milkdown.toggleHighlightCommand),
    InlineCode: callCommand(Milkdown.toggleInlineCodeCommand),
    Heading: (editor, view, linePrefix) => isInside(view, nodeTypes.heading)
        ? run(editor, Milkdown.turnIntoTextCommand)
        : run(editor, Milkdown.wrapInHeadingCommand, linePrefix.trim().length),
    BulletedList: (editor, view) => isInside(view, nodeTypes.bulletList) && isTask(view) ? setTaskState(view, null) : toggleBulletList(editor, view),
    NumberedList: (editor, view) => toggleList(editor, view, nodeTypes.orderedList, Milkdown.wrapInOrderedListCommand),
    TaskList: (editor, view) => {
        if (isTask(view)) {
            return setTaskState(view, null);
        }
        if (!isInside(view, nodeTypes.bulletList)) {
            toggleBulletList(editor, view);
        }
        return setTaskState(view, false);
    },
    Outdent: callCommand(Milkdown.liftListItemCommand),
    Indent: callCommand(Milkdown.sinkListItemCommand),
    Quote: (editor, view) => isInside(view, nodeTypes.blockquote) ? Milkdown.lift(view.state, view.dispatch) : run(editor, Milkdown.wrapInBlockquoteCommand),
    // Links the selection, or placeholder text when nothing is selected, and opens the link box to type the address into.
    Link: (editor, view, linePrefix, placeholderText) => {
        let { from, to } = view.state.selection;
        if (from === to) {
            view.dispatch(view.state.tr.insertText(placeholderText, from));
            to = from + placeholderText.length;
        }
        editor.action((ctx) => ctx.get(Milkdown.linkTooltipAPI.key).addLink(from, to));
    }
};

const withEditor = async (elementId, use) => {
    const entry = editors.get(elementId);
    if (!entry) {
        return;
    }
    await entry.ready;
    if (editors.get(elementId) === entry) {
        use(entry);
    }
};

const viewOf = (entry) => entry.builder.editor.action((ctx) => ctx.get(Milkdown.editorViewCtx));

// The options carry placeholderText, maxLength and italicMarker.
export function create(elementId, markdown, options, dotNetReference, changedMethodName) {
    const root = document.getElementById(elementId);
    if (!root || editors.has(elementId)) {
        return;
    }

    const entry = { markdown: markdown ?? '', maxLength: options.maxLength, dotNetReference, changedMethodName, applying: false, serialize: null, markdownByDoc: new WeakMap() };
    const builder = new Milkdown.CrepeBuilder({ root, defaultValue: entry.markdown });
    builder.editor.config(configure(entry, options.italicMarker)).use(Milkdown.highlight);
    builder.addFeature(Milkdown.listItem).addFeature(Milkdown.linkTooltip).addFeature(Milkdown.cursor).addFeature(Milkdown.placeholder, { text: options.placeholderText, mode: 'doc' });
    entry.builder = builder;
    entry.ready = builder.create().then(() => {
        entry.serialize = builder.editor.action((ctx) => ctx.get(Milkdown.serializerCtx));
    });
    editors.set(elementId, entry);
}

// Replaces the text after Blazor changed it itself, without reporting it back as an edit.
export function setMarkdown(elementId, markdown) {
    return withEditor(elementId, (entry) => {
        entry.applying = true;
        try {
            entry.builder.editor.action(Milkdown.replaceAll(markdown ?? ''));
        } finally {
            entry.applying = false;
        }
        entry.markdown = markdownOf(entry, viewOf(entry).state.doc);
    });
}

export function apply(elementId, action, linePrefix, placeholderText) {
    return withEditor(elementId, (entry) => {
        const view = viewOf(entry);
        view.focus();
        actions[action]?.(entry.builder.editor, view, linePrefix ?? '', placeholderText ?? '');
    });
}

export async function destroy(elementId) {
    const entry = editors.get(elementId);
    if (!entry) {
        return;
    }
    editors.delete(elementId);
    await entry.ready;
    await entry.builder.destroy();
}
