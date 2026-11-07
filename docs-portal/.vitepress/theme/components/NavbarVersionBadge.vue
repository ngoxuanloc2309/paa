<template>
  <a
    href="https://github.com/hoanv-synaptix/SynaptiX-IDE-Releases/releases/tag/v2.11.4"
    target="_blank"
    rel="noopener noreferrer"
    class="synx-nav-version-badge"
    title="Official SynaptiX IDE Release v2.11.4"
  >
    <span class="synx-dot"></span>
    <span class="synx-version-num">v2.11.4</span>
  </a>
</template>

<style scoped>
.synx-nav-version-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  height: 26px;
  padding: 0 8px;
  border-radius: 4px;
  background-color: var(--vp-c-bg-alt);
  border: 1px solid var(--vp-c-divider);
  font-family: var(--vp-font-family-mono);
  font-size: 0.74rem;
  font-weight: 600;
  color: var(--vp-c-text-2);
  text-decoration: none !important;
  transition: all 0.16s ease;
  white-space: nowrap;
}

.synx-nav-version-badge:hover {
  background-color: var(--vp-c-brand-soft);
  color: var(--vp-c-brand-1);
  border-color: var(--vp-c-brand-1);
}

.synx-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: #107c41;
}

.synx-version-num {
  letter-spacing: 0.02em;
}
</style>
