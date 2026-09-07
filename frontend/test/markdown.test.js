import assert from 'node:assert/strict'
import test from 'node:test'

import { renderMarkdown } from '../src/utils/markdown.js'

test('renders common Markdown syntax', () => {
  const markdown = [
    '# 标题',
    '',
    '- **加粗项目**',
    '- `inline_code`',
    '',
    '```js',
    'const answer = 42',
    '```',
  ].join('\n')

  const html = renderMarkdown(markdown)
  assert.match(html, /<h1>标题<\/h1>/)
  assert.match(html, /<ul>[\s\S]*<strong>加粗项目<\/strong>[\s\S]*<code>inline_code<\/code>[\s\S]*<\/ul>/)
  assert.match(html, /<pre><code>const answer = 42<\/code><\/pre>/)
})

test('escapes HTML and rejects unsafe link protocols', () => {
  const html = renderMarkdown('<img src=x onerror=alert(1)>\n[x](javascript:alert)')

  assert.doesNotMatch(html, /<img/)
  assert.match(html, /&lt;img src=x onerror=alert\(1\)&gt;/)
  assert.match(html, /href="#"/)
  assert.doesNotMatch(html, /href="javascript:/)
})
