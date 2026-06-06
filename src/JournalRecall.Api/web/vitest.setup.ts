import '@testing-library/jest-dom/vitest'

// jsdom doesn't implement Document.elementFromPoint, which ProseMirror's viewport tracking
// (used by the tiptap Placeholder extension) calls during editor mount. Stub it so rich-text
// editor components can mount in jsdom; coordinate-based hit-testing is irrelevant in unit tests.
if (typeof document !== 'undefined' && typeof document.elementFromPoint !== 'function') {
  document.elementFromPoint = () => null
}

// ProseMirror's scrollToSelection / coordsAtPos read layout via getClientRects, which jsdom leaves
// unimplemented (returns nothing useful). Provide empty/zero geometry so the editor can update state
// in jsdom without throwing. Real layout is exercised by the browser-driven functional tests.
const EMPTY_RECT = {
  x: 0, y: 0, width: 0, height: 0, top: 0, right: 0, bottom: 0, left: 0,
  toJSON: () => ({}),
} as DOMRect
if (typeof Range !== 'undefined') {
  Range.prototype.getClientRects = () =>
    ({ length: 0, item: () => null, [Symbol.iterator]: function* () {} }) as unknown as DOMRectList
  Range.prototype.getBoundingClientRect = () => EMPTY_RECT
}
