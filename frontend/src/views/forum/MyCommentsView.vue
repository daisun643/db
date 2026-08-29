<template>
  <section class="my-empty-page">
    <h2>我的评论</h2>
    <p>你发表过的评论会显示在这里。</p>
    <div class="empty-state">
      <p>暂无评论</p>
    </div>
  </section>
</template>

<style scoped>
.my-empty-page {
  padding: 1.4rem;
  border: 1px solid #e4e7ec;
  border-radius: 14px;
  background: #fff;
}

.my-empty-page h2 {
  margin: 0;
  color: var(--forum-ink, #11172a);
  font-size: 1.25rem;
}

.my-empty-page > p {
  margin: .4rem 0 1.2rem;
  color: #778398;
  font-size: .82rem;
}

.empty-state {
  padding: 3rem 1rem;
  color: var(--text-secondary);
  text-align: center;
}
</style>
