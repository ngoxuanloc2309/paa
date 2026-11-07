<script setup lang="ts">
import { ref, computed, shallowRef, markRaw, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useData, useRouter } from 'vitepress'
import localSearchIndex from '@localSearchIndex'
import MiniSearch, { type SearchResult } from 'minisearch'

interface DocSearchResult {
  id: string
  title: string
  titles: string[]
  score?: number
}

const { localeIndex, theme } = useData()
const router = useRouter()

const searchIndexData = shallowRef(localSearchIndex)

if (import.meta.hot) {
  import.meta.hot.accept('/@localSearchIndex', (m) => {
    if (m) {
      searchIndexData.value = m.default
    }
  })
}

const query = ref('')
const isOpen = ref(false)
const selectedIndex = ref(-1)
const results = ref<DocSearchResult[]>([])
const searchIndex = shallowRef<MiniSearch<DocSearchResult> | null>(null)
const inputRef = ref<HTMLInputElement | null>(null)
const containerRef = ref<HTMLElement | null>(null)
const resultsListRef = ref<HTMLElement | null>(null)

// Lazy load search index for current locale
async function ensureIndex() {
  if (searchIndex.value) return
  const loader = searchIndexData.value[localeIndex.value]
  if (!loader) return

  try {
    const rawData = (await loader())?.default
    if (!rawData) return

    const index = MiniSearch.loadJSON<DocSearchResult>(rawData, {
      fields: ['title', 'titles', 'text'],
      storeFields: ['title', 'titles'],
      searchOptions: {
        fuzzy: 0.2,
        prefix: true,
        boost: { title: 4, text: 2, titles: 1 },
        ...(theme.value.search?.provider === 'local' &&
          theme.value.search.options?.miniSearch?.searchOptions)
      },
      ...(theme.value.search?.provider === 'local' &&
        theme.value.search.options?.miniSearch?.options)
    })
    searchIndex.value = markRaw(index)
  } catch (err) {
    console.error('Failed to load search index:', err)
  }
}

// Watch locale changes to reload index
watch(localeIndex, () => {
  searchIndex.value = null
  if (isOpen.value) {
    ensureIndex().then(executeSearch)
  }
})

// Execute search
function executeSearch() {
  const text = query.value.trim()
  if (!text || !searchIndex.value) {
    results.value = []
    selectedIndex.value = -1
    return
  }

  try {
    const rawResults = searchIndex.value.search(text)
    // Filter and take top 12 results
    results.value = rawResults.slice(0, 12).map((r) => ({
      id: r.id,
      title: r.title || 'Untitled',
      titles: r.titles || [],
      score: r.score
    }))
    selectedIndex.value = results.value.length > 0 ? 0 : -1
  } catch {
    results.value = []
    selectedIndex.value = -1
  }
}

watch(query, () => {
  executeSearch()
})

const isVietnamese = computed(() => localeIndex.value === 'vi')

const placeholderText = computed(() => {
  return isVietnamese.value ? 'Tìm kiếm' : 'Search'
})

const noResultsText = computed(() => {
  return isVietnamese.value
    ? 'Không tìm thấy kết quả phù hợp'
    : 'No matching results found'
})

const footerHintText = computed(() => {
  return isVietnamese.value
    ? '↑↓ để duyệt • Enter để chọn • Esc để đóng'
    : '↑↓ to navigate • Enter to select • Esc to close'
})

async function onFocus() {
  isOpen.value = true
  await ensureIndex()
  if (query.value.trim()) {
    executeSearch()
  }
}

function clearQuery() {
  query.value = ''
  results.value = []
  selectedIndex.value = -1
  inputRef.value?.focus()
}

function navigateTo(id: string) {
  isOpen.value = false
  query.value = ''
  results.value = []
  selectedIndex.value = -1
  inputRef.value?.blur()
  router.go(id)
}

function onKeyDown(e: KeyboardEvent) {
  if (e.key === 'Escape') {
    isOpen.value = false
    inputRef.value?.blur()
    return
  }

  if (!isOpen.value) {
    if (e.key === 'ArrowDown' || e.key === 'Enter') {
      isOpen.value = true
      onFocus()
    }
    return
  }

  if (e.key === 'ArrowDown') {
    e.preventDefault()
    if (results.value.length > 0) {
      selectedIndex.value = (selectedIndex.value + 1) % results.value.length
      scrollToSelected()
    }
  } else if (e.key === 'ArrowUp') {
    e.preventDefault()
    if (results.value.length > 0) {
      selectedIndex.value =
        (selectedIndex.value - 1 + results.value.length) % results.value.length
      scrollToSelected()
    }
  } else if (e.key === 'Enter') {
    e.preventDefault()
    if (selectedIndex.value >= 0 && selectedIndex.value < results.value.length) {
      navigateTo(results.value[selectedIndex.value].id)
    }
  }
}

function scrollToSelected() {
  nextTick(() => {
    const activeEl = resultsListRef.value?.querySelector('.synx-result-item.is-selected') as HTMLElement
    if (activeEl) {
      activeEl.scrollIntoView({ block: 'nearest' })
    }
  })
}

// Global shortcut handler (Ctrl+K or /)
function handleGlobalHotKey(e: KeyboardEvent) {
  const isCtrlK = (e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k'
  const isSlash = e.key === '/' && !['INPUT', 'TEXTAREA'].includes((e.target as HTMLElement)?.tagName)

  if (isCtrlK || isSlash) {
    e.preventDefault()
    inputRef.value?.focus()
    inputRef.value?.select()
    isOpen.value = true
  }
}

// Click outside handler
function handleClickOutside(e: MouseEvent) {
  if (containerRef.value && !containerRef.value.contains(e.target as Node)) {
    isOpen.value = false
  }
}

onMounted(() => {
  window.addEventListener('keydown', handleGlobalHotKey)
  window.addEventListener('pointerdown', handleClickOutside)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleGlobalHotKey)
  window.removeEventListener('pointerdown', handleClickOutside)
})
</script>

<template>
  <div class="VPNavBarSearch synx-inline-search-container" ref="containerRef">
    <div class="synx-search-box" :class="{ 'is-active': isOpen }">
      <svg class="synx-search-svg" viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="11" cy="11" r="8"></circle>
        <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
      </svg>
      <input
        ref="inputRef"
        v-model="query"
        type="search"
        class="synx-search-input"
        :placeholder="placeholderText"
        autocomplete="off"
        autocorrect="off"
        autocapitalize="off"
        spellcheck="false"
        @focus="onFocus"
        @keydown="onKeyDown"
      />
      <div v-if="!query" class="synx-search-shortcut" @click="inputRef?.focus()">
        <kbd>Ctrl</kbd>
        <kbd>K</kbd>
      </div>
      <button
        v-else
        type="button"
        class="synx-search-clear-btn"
        title="Clear"
        @click="clearQuery"
      >
        <svg viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="18" y1="6" x2="6" y2="18"></line>
          <line x1="6" y1="6" x2="18" y2="18"></line>
        </svg>
      </button>
    </div>

    <!-- Anchored Dropdown Menu (Directly attached below input, zero screen-blocking popup) -->
    <div
      v-if="isOpen && query.trim()"
      class="synx-search-dropdown"
    >
      <div v-if="results.length > 0" class="synx-results-list" ref="resultsListRef">
        <div
          v-for="(item, idx) in results"
          :key="item.id"
          class="synx-result-item"
          :class="{ 'is-selected': idx === selectedIndex }"
          @click="navigateTo(item.id)"
          @mouseenter="selectedIndex = idx"
        >
          <div class="synx-result-header" v-if="item.titles && item.titles.length">
            <span class="synx-result-category">{{ item.titles.join('  ›  ') }}</span>
          </div>
          <div class="synx-result-title">
            <svg class="synx-doc-icon" viewBox="0 0 24 24" width="13" height="13" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
            <span class="synx-title-text">{{ item.title }}</span>
          </div>
        </div>
      </div>

      <div v-else class="synx-no-results">
        <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" class="synx-empty-icon">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          <line x1="8" y1="11" x2="14" y2="11"></line>
        </svg>
        <span>{{ noResultsText }}</span>
      </div>

      <div class="synx-search-footer">
        <span>{{ footerHintText }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.synx-inline-search-container {
  position: relative;
  display: flex;
  align-items: center;
  flex-grow: 0 !important;
  flex-shrink: 0 !important;
  padding-left: 0 !important;
  z-index: 50;
}

.synx-search-box {
  display: flex;
  align-items: center;
  position: relative;
  width: 220px;
  height: 34px;
  background-color: var(--vp-c-bg-alt);
  border: 1px solid var(--vp-c-divider);
  border-radius: 6px;
  padding: 0 8px;
  transition: width 0.2s cubic-bezier(0.16, 1, 0.3, 1),
              border-color 0.2s ease,
              background-color 0.2s ease,
              box-shadow 0.2s ease;
  box-sizing: border-box;
}

.synx-search-box:hover {
  border-color: var(--vp-c-brand-1);
  background-color: var(--vp-c-bg-soft);
}

.synx-search-box.is-active {
  width: 280px;
  border-color: var(--vp-c-brand-1);
  background-color: var(--vp-c-bg);
  box-shadow: 0 0 0 2px rgba(0, 100, 135, 0.2);
}

.synx-search-svg {
  color: var(--vp-c-text-3);
  margin-right: 6px;
  flex-shrink: 0;
  transition: color 0.2s ease;
}

.synx-search-box.is-active .synx-search-svg,
.synx-search-box:hover .synx-search-svg {
  color: var(--vp-c-brand-1);
}

.synx-search-input {
  flex-grow: 1;
  min-width: 0;
  height: 100%;
  border: none !important;
  outline: none !important;
  background: transparent !important;
  color: var(--vp-c-text-1);
  font-size: 0.84rem;
  font-family: inherit;
  padding: 0;
}

.synx-search-input::placeholder {
  color: var(--vp-c-text-3);
  font-size: 0.82rem;
}

/* Hide default browser cancel button in input[type=search] */
.synx-search-input::-webkit-search-cancel-button {
  -webkit-appearance: none;
}

.synx-search-shortcut {
  display: flex;
  align-items: center;
  gap: 3px;
  user-select: none;
  cursor: pointer;
  flex-shrink: 0;
}

.synx-search-shortcut kbd {
  display: inline-block;
  padding: 1px 4px;
  font-family: var(--vp-font-family-mono);
  font-size: 0.72rem;
  font-weight: 600;
  line-height: 1.2;
  color: var(--vp-c-text-3);
  background-color: var(--vp-c-bg);
  border: 1px solid var(--vp-c-divider);
  border-radius: 4px;
}

.synx-search-clear-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  border: none;
  background: transparent;
  color: var(--vp-c-text-3);
  border-radius: 3px;
  cursor: pointer;
  padding: 0;
  transition: color 0.15s ease, background-color 0.15s ease;
}

.synx-search-clear-btn:hover {
  color: var(--vp-c-text-1);
  background-color: var(--vp-c-default-soft);
}

/* ==========================================================================
   DROPDOWN RESULTS (Anchored directly below input, zero screen darkening)
   ========================================================================== */
.synx-search-dropdown {
  position: absolute;
  top: calc(100% + 6px);
  left: 0;
  width: 480px;
  max-width: calc(100vw - 32px);
  background-color: var(--vp-c-bg-elv, var(--vp-c-bg));
  border: 1px solid var(--vp-c-divider);
  border-radius: 6px;
  box-shadow: 0 10px 28px rgba(0, 0, 0, 0.18), 0 2px 6px rgba(0, 0, 0, 0.08);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  z-index: 1000;
  animation: synx-dropdown-fade-in 0.15s ease;
}

@keyframes synx-dropdown-fade-in {
  from {
    opacity: 0;
    transform: translateY(-4px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.synx-results-list {
  max-height: 380px;
  overflow-y: auto;
  padding: 6px;
}

.synx-result-item {
  padding: 8px 12px;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.12s ease, border-left-color 0.12s ease;
  border-left: 3px solid transparent;
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.synx-result-item:hover,
.synx-result-item.is-selected {
  background-color: var(--vp-c-brand-soft);
  border-left-color: var(--vp-c-brand-1);
}

.synx-result-header {
  display: flex;
  align-items: center;
}

.synx-result-category {
  font-size: 0.73rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  color: var(--vp-c-text-3);
}

.synx-result-item.is-selected .synx-result-category {
  color: var(--vp-c-brand-1);
}

.synx-result-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.synx-doc-icon {
  color: var(--vp-c-text-3);
  flex-shrink: 0;
}

.synx-result-item.is-selected .synx-doc-icon {
  color: var(--vp-c-brand-1);
}

.synx-title-text {
  font-size: 0.88rem;
  font-weight: 500;
  color: var(--vp-c-text-1);
  line-height: 1.35;
}

.synx-result-item.is-selected .synx-title-text {
  color: var(--vp-c-brand-1);
  font-weight: 600;
}

.synx-no-results {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 28px 16px;
  color: var(--vp-c-text-3);
  font-size: 0.86rem;
  gap: 8px;
  text-align: center;
}

.synx-empty-icon {
  opacity: 0.6;
}

.synx-search-footer {
  padding: 7px 12px;
  background-color: var(--vp-c-bg-alt);
  border-top: 1px solid var(--vp-c-divider);
  font-size: 0.73rem;
  color: var(--vp-c-text-3);
  display: flex;
  align-items: center;
  justify-content: flex-end;
  user-select: none;
}

@media (max-width: 767px) {
  .synx-search-box {
    width: 140px;
  }
  .synx-search-box.is-active {
    width: 200px;
  }
  .synx-search-dropdown {
    width: 320px;
    left: -60px;
  }
}
</style>
