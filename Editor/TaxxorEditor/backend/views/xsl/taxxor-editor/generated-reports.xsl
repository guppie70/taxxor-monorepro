<?xml version='1.0'?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">

	<xsl:param name="project-id"/>
	<xsl:param name="token"/>

	<xsl:output method="xml" indent="yes" encoding="UTF-8" omit-xml-declaration="yes"/>


	<xsl:template match="/">
		<div>
			<xsl:if test="/reports/report">
				<h5><xsl:value-of select="/reports/report/@scheme-name"/> generation history</h5>
			</xsl:if>
			<table class="table table-condensed">
				<thead>
					<tr>
						<th class="tx-timestamp">Timestamp</th>
						<th class="tx-user">User</th>
						<th class="tx-comment">Comment</th>
						<th class="tx-action">Action</th>
					</tr>
				</thead>
				<tbody>
					<xsl:choose>
						<xsl:when test="/reports/report">
							<xsl:apply-templates select="/reports/report">
								<xsl:sort select="@epoch" data-type="number" order="descending"/>
							</xsl:apply-templates>
						</xsl:when>
						<xsl:otherwise>
							<tr>
								<td colspan="5">No generated reports available yet</td>
							</tr>
						</xsl:otherwise>
					</xsl:choose>
				</tbody>
			</table>
		</div>

	</xsl:template>

	<xsl:template match="report">
		<tr>
			<td data-epoch="{@epoch}"> render a date stamp here </td>
			<td>
				<xsl:value-of select="@user-fullname"/>
			</td>
			<td>
				<xsl:if test="string-length(comment) &gt; 0">
					<pre>
						<xsl:value-of select="comment"/>
					</pre>
				</xsl:if>
			</td>
			<td class="tx-action">
				<a href="/api/repository/generatedreports?pid={$project-id}&amp;guid={@id}&amp;nekot={$token}">Download package</a>
				<xsl:for-each select="validationlinks/a">
					<xsl:sort select="text()"/>
					<br/>
					<xsl:copy-of select="."/>
				</xsl:for-each>
			</td>
		</tr>
	</xsl:template>



</xsl:stylesheet>
