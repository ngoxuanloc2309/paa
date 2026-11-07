<script setup lang="ts">
import { computed } from 'vue'
import { useData, useRoute, withBase } from 'vitepress'

const { page, lang } = useData()
const route = useRoute()

const isVi = computed(() => lang.value === 'vi-VN' || lang.value === 'vi')

// Do not render breadcrumbs on root landing pages
const isHomePage = computed(() => {
  const p = route.path.replace(/\/index(\.html)?$/, '').replace(/\/$/, '')
  return p === '' || p === '/vi' || p.endsWith('/SynaptiX-IDE-Releases') || p.endsWith('/SynaptiX-IDE-Releases/vi')
})

interface ChapterMeta {
  code: string
  titleEn: string
  titleVi: string
  linkEn: string
  linkVi: string
}

const chapters: Record<string, ChapterMeta> = {
  'concepts': {
    code: '01',
    titleEn: 'Introduction & Core Concepts',
    titleVi: 'Tổng Quan & Kiến Trúc Cốt Lõi',
    linkEn: '/concepts/ecosystem-overview',
    linkVi: '/vi/concepts/ecosystem-overview'
  },
  'getting-started': {
    code: '02',
    titleEn: 'Installation & Quickstart',
    titleVi: 'Cài Đặt & Khởi Tạo Nhanh',
    linkEn: '/getting-started/installation',
    linkVi: '/vi/getting-started/installation'
  },
  'workspaces': {
    code: '03',
    titleEn: 'IDE Workspaces (7 Tabs)',
    titleVi: 'Không Gian Làm Việc (7 Tab)',
    linkEn: '/workspaces/device-tab',
    linkVi: '/vi/workspaces/device-tab'
  },
  'blocks': {
    code: '04',
    titleEn: 'Function Blocks Reference',
    titleVi: 'Từ Điển Khối Hàm Chuẩn',
    linkEn: '/blocks/overview',
    linkVi: '/vi/blocks/overview'
  },
  'tools': {
    code: '05',
    titleEn: 'Engineering Tools & Protocols',
    titleVi: 'Công Cụ Kỹ Thuật & Giao Thức',
    linkEn: '/tools/rs485-and-scada-slave',
    linkVi: '/vi/tools/rs485-and-scada-slave'
  },
  'appendices': {
    code: '06',
    titleEn: 'Technical Appendices',
    titleVi: 'Phụ Lục Kỹ Thuật',
    linkEn: '/appendices/keyboard-shortcuts',
    linkVi: '/vi/appendices/keyboard-shortcuts'
  }
}

const currentChapter = computed<ChapterMeta | null>(() => {
  const path = route.path
  for (const [key, meta] of Object.entries(chapters)) {
    if (path.includes(`/${key}/`)) {
      return meta
    }
  }
  return null
})

const pageTitle = computed(() => {
  return page.value.title || ''
})
</script>

<template>
  <nav v-if="!isHomePage && currentChapter" class="synx-breadcrumb-nav" aria-label="Breadcrumb">
    <ol class="synx-breadcrumb-list">
      <li class="synx-breadcrumb-item">
        <a :href="withBase(isVi ? '/vi/' : '/')" class="synx-breadcrumb-link synx-breadcrumb-home">
          <svg viewBox="0 0 24 24" width="13" height="13" class="synx-breadcrumb-icon">
            <path fill="currentColor" d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/>
          </svg>
          <span>{{ isVi ? 'Trang Chủ' : 'Guide Home' }}</span>
        </a>
      </li>

      <li class="synx-breadcrumb-separator" aria-hidden="true">
        <svg viewBox="0 0 24 24" width="12" height="12">
          <path fill="currentColor" d="M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z"/>
        </svg>
      </li>

      <li class="synx-breadcrumb-item">
        <a :href="withBase(isVi ? currentChapter.linkVi : currentChapter.linkEn)" class="synx-breadcrumb-link">
          <span class="synx-chapter-code">{{ currentChapter.code }}.</span>
          <span>{{ isVi ? currentChapter.titleVi : currentChapter.titleEn }}</span>
        </a>
      </li>

      <li class="synx-breadcrumb-separator" aria-hidden="true">
        <svg viewBox="0 0 24 24" width="12" height="12">
          <path fill="currentColor" d="M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z"/>
        </svg>
      </li>

      <li class="synx-breadcrumb-item current" aria-current="page">
        <span class="synx-breadcrumb-current">{{ pageTitle }}</span>
      </li>
    </ol>
  </nav>
</template>

<style scoped>
.synx-breadcrumb-nav {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  padding: 8px 0 16px 0;
  margin-bottom: 8px;
  border-bottom: 1px solid var(--vp-c-divider);
  font-size: 0.81rem;
  line-height: 1.4;
}

.synx-breadcrumb-list {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 6px;
  list-style: none;
  margin: 0;
  padding: 0;
}

.synx-breadcrumb-item {
  display: inline-flex;
  align-items: center;
}

.synx-breadcrumb-link {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--vp-c-text-2);
  text-decoration: none !important;
  transition: color 0.16s ease;
  font-weight: 500;
}

.synx-breadcrumb-link:hover {
  color: var(--vp-c-brand-1);
}

.synx-breadcrumb-icon {
  opacity: 0.75;
}

.synx-chapter-code {
  font-family: var(--vp-font-family-mono);
  font-weight: 700;
  color: var(--vp-c-brand-1);
}

.synx-breadcrumb-separator {
  display: inline-flex;
  align-items: center;
  color: var(--vp-c-text-3);
  opacity: 0.55;
  user-select: none;
}

.synx-breadcrumb-current {
  color: var(--vp-c-text-1);
  font-weight: 600;
  max-width: 320px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.synx-breadcrumb-actions {
  display: flex;
  align-items: center;
  flex-shrink: 0;
  margin-left: 12px;
}

.synx-source-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 2px 8px;
  border-radius: 4px;
  background-color: var(--vp-c-bg-soft);
  border: 1px solid var(--vp-c-divider);
  font-family: var(--vp-font-family-mono);
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--vp-c-text-2);
  text-decoration: none !important;
  transition: all 0.16s ease;
}

.synx-source-pill:hover {
  background-color: var(--vp-c-brand-soft);
  color: var(--vp-c-brand-1);
  border-color: var(--vp-c-brand-1);
}

.synx-pulse-indicator {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: #107c41;
}

@media (max-width: 768px) {
  .synx-breadcrumb-actions {
    display: none;
  }
}
</style>
