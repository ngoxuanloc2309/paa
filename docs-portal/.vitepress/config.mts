import { defineConfig } from 'vitepress'
import { fileURLToPath, URL } from 'node:url'
import { enConfig } from './locales/en.mts'
import { viConfig } from './locales/vi.mts'

export default defineConfig({
  base: process.env.VITEPRESS_BASE || '/',
  cleanUrls: true,
  lastUpdated: true,

  head: [
    ['link', { rel: 'icon', type: 'image/png', href: `${process.env.VITEPRESS_BASE || '/'}logo.png` }]
  ],

  rewrites: {
    'en/:rest*': ':rest*'
  },

  markdown: {
    math: true,
    lineNumbers: false,
    config(md) {
      const defaultFence = md.renderer.rules.fence?.bind(md.renderer.rules)
      md.renderer.rules.fence = (tokens, idx, options, env, self) => {
        const token = tokens[idx]
        const info = token.info.trim()
        if (info === 'mermaid') {
          const encoded = encodeURIComponent(token.content)
          return `<MermaidDiagram id="mermaid-${idx}" code="${encoded}" />`
        }
        return defaultFence ? defaultFence(tokens, idx, options, env, self) : self.renderToken(tokens, idx, options)
      }
    }
  },

  locales: {
    root: {
      label: 'English',
      lang: 'en-US',
      ...enConfig
    },
    vi: {
      label: 'Tiếng Việt',
      lang: 'vi-VN',
      link: '/vi/',
      ...viConfig
    }
  },

  themeConfig: {
    logo: '/logo.png',
    siteTitle: 'SynaptiX Programming Guide',
    search: {
      provider: 'local',
      options: {
        locales: {
          vi: {
            translations: {
              button: {
                buttonText: 'Tìm kiếm',
                buttonAriaLabel: 'Tìm kiếm'
              },
              modal: {
                noResultsText: 'Không tìm thấy kết quả phù hợp cho',
                resetButtonTitle: 'Xóa truy vấn',
                footer: {
                  selectText: 'để chọn',
                  navigateText: 'để di chuyển',
                  closeText: 'để đóng'
                }
              }
            }
          }
        }
      }
    }
  },

  vite: {
    resolve: {
      alias: [
        {
          find: /^.*\/VPNavBarSearch\.vue$/,
          replacement: fileURLToPath(new URL('./theme/components/NavbarInlineSearch.vue', import.meta.url))
        }
      ]
    },
    server: {
      host: true
    },
    preview: {
      host: true
    },
    plugins: [
      {
        name: 'synaptix-normalize-windows-fs-url',
        transformIndexHtml(html) {
          return html.replace(/src="\/@fs\/([^"]+)"/g, (_match, fsPath: string) => {
            return `src="/@fs/${fsPath.replace(/\\/g, '/')}"`
          })
        }
      }
    ],
    optimizeDeps: {
      include: ['mermaid']
    },
    build: {
      chunkSizeWarningLimit: 2000
    }
  }
})
