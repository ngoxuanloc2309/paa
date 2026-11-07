<script setup lang="ts">
import DefaultTheme from 'vitepress/theme'
import NavbarVersionBadge from './components/NavbarVersionBadge.vue'
import DocBreadcrumb from './components/DocBreadcrumb.vue'

const { Layout } = DefaultTheme
</script>

<template>
  <Layout>
    <template #nav-bar-content-after>
      <NavbarVersionBadge />
    </template>
    <template #doc-before>
      <DocBreadcrumb />
    </template>
  </Layout>
</template>
