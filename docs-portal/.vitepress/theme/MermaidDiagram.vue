<script setup lang="ts">
import { onMounted, onUnmounted, ref, nextTick } from 'vue'

const props = defineProps<{
  id: string
  code: string
}>()

const svgHtml = ref<string>('')
const errorText = ref<string>('')
const isFullscreen = ref(false)
const copied = ref(false)
let observer: MutationObserver | null = null

async function renderDiagram() {
  try {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await document.fonts.ready
    }
    const { default: mermaid } = await import('mermaid')
    const isDark = document.documentElement.classList.contains('dark')
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'loose',
      theme: 'base',
      fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
      fontSize: 13,
      flowchart: {
        htmlLabels: false,
        useMaxWidth: true,
        curve: 'linear',
        padding: 20,
        nodeSpacing: 40,
        rankSpacing: 40
      },
      themeVariables: isDark
        ? {
            darkMode: true,
            background: '#0a0a0a',
            mainBkg: '#141414',
            primaryColor: '#181818',
            primaryTextColor: '#ededed',
            primaryBorderColor: '#2e2e2e',
            lineColor: '#60a5fa',
            secondaryColor: '#121212',
            tertiaryColor: '#0a0a0a',
            clusterBkg: '#0d0d0d',
            clusterBorder: '#262626',
            titleColor: '#ededed',
            edgeLabelBackground: '#1a1a1a',
            nodeBorder: '#333333',
            defaultLinkColor: '#888888',
            fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
            fontSize: '13px'
          }
        : {
            darkMode: false,
            background: '#ffffff',
            mainBkg: '#ffffff',
            primaryColor: '#ffffff',
            primaryTextColor: '#0f172a',
            primaryBorderColor: '#cbd5e1',
            lineColor: '#006487',
            secondaryColor: '#f8fafc',
            tertiaryColor: '#f1f5f9',
            clusterBkg: '#f8fafc',
            clusterBorder: '#cbd5e1',
            titleColor: '#0f172a',
            edgeLabelBackground: '#ffffff',
            nodeBorder: '#cbd5e1',
            defaultLinkColor: '#475569',
            fontFamily: 'Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
            fontSize: '13px'
          }
    })
    const decoded = decodeURIComponent(props.code)
    const uniqueId = `${props.id}-${Math.random().toString(36).slice(2, 8)}`
    const { svg } = await mermaid.render(uniqueId, decoded)
    svgHtml.value = svg
    errorText.value = ''
  } catch (err) {
    errorText.value = err instanceof Error ? err.message : String(err)
  }
}

function openFullscreen() {
  isFullscreen.value = true
}

function closeFullscreen() {
  isFullscreen.value = false
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && isFullscreen.value) {
    closeFullscreen()
  }
}

async function copyCode() {
  try {
    await navigator.clipboard.writeText(decodeURIComponent(props.code))
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch {
    // fallback
  }
}

onMounted(async () => {
  await renderDiagram()
  observer = new MutationObserver(() => {
    void renderDiagram()
  })
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['class']
  })
  window.addEventListener('keydown', onKeydown)
})

onUnmounted(() => {
  observer?.disconnect()
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div class="synx-mermaid-container">
    <div class="synx-mermaid-toolbar">
      <button
        type="button"
        class="synx-mermaid-btn"
        :title="copied ? 'Đã sao chép' : 'Sao chép Mermaid code'"
        @click="copyCode"
      >
        <svg v-if="!copied" viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2">
          <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
          <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
        </svg>
        <svg v-else viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="#107c41" stroke-width="2">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
        <span>{{ copied ? 'Copied' : 'Copy' }}</span>
      </button>

      <button
        type="button"
        class="synx-mermaid-btn"
        title="Xem Toàn màn hình (Fullscreen)"
        @click="openFullscreen"
      >
        <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2">
          <polyline points="15 3 21 3 21 9"></polyline>
          <polyline points="9 21 3 21 3 15"></polyline>
          <line x1="21" y1="3" x2="14" y2="10"></line>
          <line x1="3" y1="21" x2="10" y2="14"></line>
        </svg>
        <span>Zoom</span>
      </button>
    </div>

    <div v-if="svgHtml" class="synx-mermaid-svg" v-html="svgHtml" />
    <pre v-else-if="errorText" class="synx-mermaid-fallback">{{ decodeURIComponent(code) }}</pre>
    <div v-else class="synx-mermaid-loading">Rendering diagram…</div>

    <!-- Fullscreen Modal View -->
    <Teleport to="body">
      <div v-if="isFullscreen" class="synx-mermaid-modal" @click.self="closeFullscreen">
        <div class="synx-mermaid-modal-content">
          <div class="synx-mermaid-modal-header">
            <span class="synx-mermaid-modal-title">Sơ đồ Chi tiết • Phóng to Toàn màn hình</span>
            <button type="button" class="synx-mermaid-modal-close" @click="closeFullscreen" title="Đóng (Esc)">
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18"></line>
                <line x1="6" y1="6" x2="18" y2="18"></line>
              </svg>
            </button>
          </div>
          <div class="synx-mermaid-modal-body" v-html="svgHtml" />
        </div>
      </div>
    </Teleport>
  </div>
</template>

<style scoped>
.synx-mermaid-container {
  position: relative;
}

.synx-mermaid-toolbar {
  position: absolute;
  top: 8px;
  right: 8px;
  display: flex;
  align-items: center;
  gap: 6px;
  opacity: 0.35;
  transition: opacity 0.2s ease;
  z-index: 10;
}

.synx-mermaid-container:hover .synx-mermaid-toolbar {
  opacity: 1;
}

.synx-mermaid-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 3px 8px;
  font-size: 0.72rem;
  font-weight: 500;
  color: var(--vp-c-text-2);
  background-color: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.synx-mermaid-btn:hover {
  color: var(--vp-c-brand-1);
  border-color: var(--vp-c-brand-1);
  background-color: var(--vp-c-bg-alt);
}

/* Fullscreen Modal */
.synx-mermaid-modal {
  position: fixed;
  inset: 0;
  z-index: 9999;
  background-color: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  box-sizing: border-box;
}

.synx-mermaid-modal-content {
  background-color: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 8px;
  width: 95vw;
  max-width: 1300px;
  height: 90vh;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-shadow: 0 24px 60px rgba(0, 0, 0, 0.4);
}

.synx-mermaid-modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 18px;
  border-bottom: 1px solid var(--vp-c-divider);
  background-color: var(--vp-c-bg-alt);
}

.synx-mermaid-modal-title {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--vp-c-text-1);
}

.synx-mermaid-modal-close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 4px;
  border: 1px solid var(--vp-c-divider);
  background: transparent;
  color: var(--vp-c-text-2);
  cursor: pointer;
  transition: all 0.15s ease;
}

.synx-mermaid-modal-close:hover {
  color: var(--vp-c-text-1);
  background-color: var(--vp-c-default-soft);
}

.synx-mermaid-modal-body {
  flex-grow: 1;
  overflow: auto;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background-color: var(--vp-c-bg-soft);
}

.synx-mermaid-modal-body :deep(svg) {
  width: 100% !important;
  max-width: 100% !important;
  height: auto !important;
  max-height: 80vh !important;
}
</style>
