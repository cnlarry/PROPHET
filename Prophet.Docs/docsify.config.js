window.$docsify = {
  name: 'Prophet.Docs',
  repo: '',
  loadSidebar: '_sidebar.md',
  loadNavbar: true,
  mergeNavbar: true,
  maxLevel: 4,
  subMaxLevel: 2,
  coverpage: false,
  themeColor: '#3eaf7c',
  alias: {
    '/.*/_sidebar.md': '/_sidebar.md',
    '/.*/_navbar.md': '/_navbar.md'
  },
  search: {
    maxAge: 86400000,
    paths: 'auto',
    placeholder: '搜索文档',
    noData: '未找到结果',
    depth: 3
  }
};