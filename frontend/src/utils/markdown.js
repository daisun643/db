const escapeHtml = (value) => String(value)
  .replaceAll('&', '&amp;')
  .replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;')
  .replaceAll('"', '&quot;')
  .replaceAll("'", '&#39;')

const safeHref = (value) => {
  const href = value.trim()
  return /^(https?:\/\/|mailto:|\/|#)/i.test(href) ? href : '#'
}

const renderInline = (value) => {
  let output = escapeHtml(value)
  const protectedFragments = []
  const protect = (html) => {
    const index = protectedFragments.push(html) - 1
    return `\u0000${index}\u0000`
  }

  output = output.replace(/`([^`]+)`/g, (_match, code) => protect(`<code>${code}</code>`))
  output = output.replace(/\[([^\]]+)]\(([^)\s]+)\)/g, (_match, label, href) => protect(
    `<a href="${escapeHtml(safeHref(href))}" rel="noopener noreferrer">${label}</a>`
  ))
  output = output.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
  output = output.replace(/__([^_]+)__/g, '<strong>$1</strong>')
  output = output.replace(/(^|[^*])\*([^*]+)\*/g, '$1<em>$2</em>')
  output = output.replace(/(^|[^_])_([^_]+)_/g, '$1<em>$2</em>')
  output = output.replace(/\u0000(\d+)\u0000/g, (_match, index) => protectedFragments[Number(index)])
  return output
}

export const renderMarkdown = (source) => {
  const lines = String(source || '').replace(/\r\n?/g, '\n').split('\n')
  const html = []
  let listType = null
  let inCodeBlock = false
  let codeLines = []

  const closeList = () => {
    if (!listType) return
    html.push(`</${listType}>`)
    listType = null
  }

  for (const line of lines) {
    if (/^\s*```/.test(line)) {
      closeList()
      if (inCodeBlock) {
        html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`)
        codeLines = []
      }
      inCodeBlock = !inCodeBlock
      continue
    }

    if (inCodeBlock) {
      codeLines.push(line)
      continue
    }

    if (!line.trim()) {
      closeList()
      continue
    }

    const heading = line.match(/^(#{1,6})\s+(.+)$/)
    if (heading) {
      closeList()
      const level = heading[1].length
      html.push(`<h${level}>${renderInline(heading[2])}</h${level}>`)
      continue
    }

    const quote = line.match(/^\s*>\s?(.*)$/)
    if (quote) {
      closeList()
      html.push(`<blockquote><p>${renderInline(quote[1])}</p></blockquote>`)
      continue
    }

    const unordered = line.match(/^\s*[-+*]\s+(.+)$/)
    const ordered = line.match(/^\s*\d+[.)]\s+(.+)$/)
    const item = unordered || ordered
    if (item) {
      const nextType = unordered ? 'ul' : 'ol'
      if (listType !== nextType) {
        closeList()
        listType = nextType
        html.push(`<${listType}>`)
      }
      html.push(`<li>${renderInline(item[1])}</li>`)
      continue
    }

    closeList()
    html.push(`<p>${renderInline(line)}</p>`)
  }

  closeList()
  if (inCodeBlock) {
    html.push(`<pre><code>${escapeHtml(codeLines.join('\n'))}</code></pre>`)
  }
  return html.join('\n')
}
