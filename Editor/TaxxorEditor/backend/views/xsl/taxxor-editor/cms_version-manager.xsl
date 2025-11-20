<?xml version="1.0"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- base includes -->
	<xsl:include href="_utils.xsl"/>

	<xsl:param name="doc-configuration"/>
	<xsl:param name="doc-hierarchy"/>
	<xsl:param name="projectId"/>
	<xsl:param name="editorId"/>

	<xsl:variable name="quote">
		<xsl:text>'</xsl:text>
	</xsl:variable>



	<xsl:variable name="url-cache-viewer" select="concat($doc-hierarchy//item[@id='cms_load-cache-files']/web_page/path, '?pid=', $projectId, '&amp;filetype=pdf&amp;download=yes')"/>


	<xsl:output method="html" omit-xml-declaration="yes" indent="yes"/>


	<xsl:template match="/">
		<xsl:for-each select="tags/tag[contains(@name, '.0')]">
			<xsl:variable name="latest-version">
				<xsl:choose>
					<xsl:when test="position() = 1">yes</xsl:when>
					<xsl:otherwise>no</xsl:otherwise>
				</xsl:choose>
			</xsl:variable>
			<xsl:variable name="major-version-part" select="substring-before(@name, '.0')"/>

			<div class="timeline-container">
				<!-- render the latest version in the UI -->
				<xsl:if test="$latest-version = 'yes'">
					<xsl:apply-templates select="/tags/virtual/tag">
						<xsl:with-param name="latest-version" select="$latest-version"/>
					</xsl:apply-templates>
				</xsl:if>

				<!-- tag details -->
				<xsl:apply-templates select="/tags/tag[contains(@name, $major-version-part)]">
					<xsl:sort select="@namesort" order="descending" data-type="number"/>
				</xsl:apply-templates>

				<!-- start label -->
				<div class="timeline-label">
					<span class="label label-primary arrowed-in-right label-lg">
						<b>
							<xsl:value-of select="@name"/>
						</b>
					</span>
				</div>
			</div>
		</xsl:for-each>

	</xsl:template>

	<!-- version/tag details -->
	<xsl:template match="tag">
		<xsl:param name="latest-version">no</xsl:param>

		<xsl:variable name="main-or-sub">
			<xsl:choose>
				<xsl:when test="contains(@name, '.0')">
					<xsl:text>main</xsl:text>
				</xsl:when>
				<xsl:otherwise>
					<xsl:text>sub</xsl:text>
				</xsl:otherwise>
			</xsl:choose>
		</xsl:variable>


		<div data-tagname="{@name}" data-hashcontent="{@hashContent}" data-hashdata="{@hashData}" data-epoch="{date/@epoch}" class="timeline-item clearfix version-{$main-or-sub}">
			<div class="timeline-info">
				<i>
					<xsl:attribute name="class">
						<xsl:text>timeline-indicator ace-icon btn btn-grey no-hover </xsl:text>
						<xsl:choose>
							<xsl:when test="@name = 'current'">glyphicon glyphicon-play-circle</xsl:when>
							<xsl:otherwise>fa fa-star</xsl:otherwise>
						</xsl:choose>
					</xsl:attribute>
				</i>
			</div>

			<div class="widget-box transparent">
				<div class="widget-header widget-header-small">
					<h5 class="widget-title smaller">
						<xsl:value-of select="@name"/>
					</h5>
					<span class="widget-toolbar no-border">
						<xsl:choose>
							<xsl:when test="date/@isodate">
								<xsl:value-of select="date/@isodate"/>
								<xsl:text> - </xsl:text>
								<xsl:value-of select="date/@time"/>
							</xsl:when>
							<xsl:otherwise>
								<xsl:value-of select="date"/>
							</xsl:otherwise>
						</xsl:choose>
					</span>
				</div>

				<div class="widget-body">
					<div class="widget-main">
						<xsl:if test="$latest-version = 'yes'">
							<xsl:attribute name="class">widget-main open</xsl:attribute>
						</xsl:if>
						<h5>
							<xsl:choose>
								<!-- Check if the message contains XML data containing more information -->
								<xsl:when test="message/data/title">
									<xsl:value-of select="message/data/title"/>
								</xsl:when>
								<xsl:when test="message/annotation">
									<xsl:value-of select="message/annotation"/>
								</xsl:when>
								<xsl:when test="message/posterpimport">
									<xsl:value-of select="message/posterpimport"/>
								</xsl:when>
								<xsl:otherwise>
									<xsl:value-of select="message"/>
								</xsl:otherwise>
							</xsl:choose>
						</h5>

						<xsl:if test="message/data/log">
							<div class="space-6"/>
							<div class="log">
								<span onclick="showlog(this)">Log information <button class="ol">Show log</button></span>
								<div class="log-content">
									<pre>
										<xsl:value-of select="message/data/log"/>
									</pre>
								</div>
							</div>
						</xsl:if>
						
						<xsl:if test="message/posterpimport">
							<div class="space-6"/>
							<div class="log">
								<span onclick="showimportlog(this)">Log information <button class="ol">Show log</button></span>
								<div class="log-content">
									<pre>...</pre>
								</div>
							</div>
							<div class="space-6"/>
						</xsl:if>
						
						<xsl:if test="not(message/posterpimport)">
							<div class="space-6"/>
							<p class="widget-title" onclick="toggle(this)">Output channel snapshots <button class="o">Open</button><button class="c">Close</button></p>
							<div class="col-xs-7 widget-content">
								<xsl:call-template name="render-outputchannel-table">
									<xsl:with-param name="tag-name" select="@name"/>
									<xsl:with-param name="hash-content" select="@hashContent"/>
									<xsl:with-param name="hash-data" select="@hashData"/>
									<xsl:with-param name="main-or-sub" select="$main-or-sub"/>
									<xsl:with-param name="nodetag" select="."/>
								</xsl:call-template>
							</div>
						</xsl:if>

					</div>
				</div>
			</div>
		</div>



	</xsl:template>

	<!-- output channel name -->
	<xsl:template name="render-outputchannel-table">
		<xsl:param name="tag-name"/>
		<xsl:param name="hash-content"/>
		<xsl:param name="hash-data"/>
		<xsl:param name="main-or-sub"/>
		<xsl:param name="nodetag"/>

		<xsl:variable name="first-verion">
			<xsl:value-of select="//tag[position() = last()]/@name"/>
		</xsl:variable>

		<xsl:variable name="javascript-object" select="concat('{', 'tagname: ', $quote, $tag-name, $quote, ', ', 'hashcontent: ', $quote, $hash-content, $quote, ', ', 'hashdata: ', $quote, $hash-data, $quote, '}')"/>

		<table class="table table-condensed">
			<thead>
				<tr>
					<th width="40%">Channel</th>
					<xsl:if test="$main-or-sub = 'main'">
						<th width="20%">Files</th>
					</xsl:if>
					<th width="60%">
						<xsl:if test="$main-or-sub = 'main'">
							<xsl:attribute name="width">40%</xsl:attribute>
						</xsl:if>
						<div class="pull-right"> Actions </div>
					</th>
				</tr>
			</thead>
			<tbody>
				<xsl:variable name="version_id" select="$doc-configuration/configuration/cms_projects/cms_project[@id = $projectId]/versions/version[position() = last()]/@id"/>
				<xsl:for-each select="$doc-configuration/configuration/editors/editor[@id = $editorId]/output_channels/output_channel">

					<xsl:variable name="output-channel-name" select="name"/>
					<xsl:variable name="output-channel-type" select="@type"/>

					<xsl:for-each select="variants/variant">
						<xsl:variable name="ocvariantid" select="@id"/>
						<xsl:variable name="language-variant" select="@lang"/>

						<xsl:variable name="params_diff_pdf" select="concat('{pid: ', $quote, $projectId, $quote, ' ,vid:', $quote, $version_id, $quote, ',ocvariantid:', $quote, $ocvariantid, $quote, ',oclang:', $quote, $language-variant, $quote, ' ,did: ', $quote, 'all', $quote, '}')"/>


						<xsl:choose>
							<xsl:when test="$nodetag/message/annotation and $main-or-sub = 'main'">
								<!-- New style tag message which contains information about the cache files rendered -->
								<xsl:if test="$nodetag/message/files/file[@ocvariantid = $ocvariantid]">
									<tr>
										<td class="col1">
											<xsl:value-of select="name"/>
										</td>
										<td class="col2">
											<xsl:for-each select="$nodetag/message/files/file[@ocvariantid = $ocvariantid]">
												<xsl:variable name="filename">
													<xsl:call-template name="get-file-name">
														<xsl:with-param name="input" select="text()"/>
													</xsl:call-template>
												</xsl:variable>
												<a href="#" target="iframe-system">
													<xsl:choose>
														<xsl:when test="@exists = 'false'"/>
														<xsl:otherwise>
															<xsl:attribute name="href">
																<xsl:value-of select="concat($url-cache-viewer, '&amp;tagname=', $tag-name, '&amp;filename=', $filename)"/>
															</xsl:attribute>
														</xsl:otherwise>
													</xsl:choose>


													<xsl:attribute name="class">
														<xsl:choose>
															<xsl:when test="@exists = 'false'">
																<xsl:text>btn btn-xs disabled btn-default</xsl:text>
															</xsl:when>
															<xsl:otherwise>
																<xsl:text>btn btn-xs btn-primary</xsl:text>
															</xsl:otherwise>
														</xsl:choose>
													</xsl:attribute>

													<xsl:choose>
														<xsl:when test="contains($filename, '.pdf')">PDF</xsl:when>
														<xsl:when test="contains($filename, '.xlsx')">Excel</xsl:when>
														<xsl:when test="contains($filename, '.docx')">Word</xsl:when>
														<xsl:when test="contains($filename, '.zip') and $output-channel-type = 'website'">ZIP package</xsl:when>
														<xsl:when test="contains($filename, '.zip') and $output-channel-type = 'pdf'">XBRL package</xsl:when>
														<xsl:otherwise>
															<xsl:value-of select="$filename"/>
														</xsl:otherwise>
													</xsl:choose>
												</a>
											</xsl:for-each>
										</td>
										<td class="col3">
											<div class="pull-right">
												<xsl:if test="not($tag-name = $first-verion)">
													<xsl:choose>
														<xsl:when test="$output-channel-type = 'pdf'">
															<button class="btn btn-xs btn-info" data-outputchanneltype="{$output-channel-type}" data-ocvariantid="{@id}" onclick="openCompareVersionModalDialog({$javascript-object}, {$params_diff_pdf}, '{name}', '{$output-channel-type}')">
																<xsl:if test="$output-channel-type = 'website'">
																	<xsl:attribute name="disabled">true</xsl:attribute>
																</xsl:if>
																<xsl:text>Compare</xsl:text>
															</button>
														</xsl:when>
														<xsl:otherwise>
															<xsl:comment>no comparison possible</xsl:comment>
														</xsl:otherwise>
													</xsl:choose>
												</xsl:if>
											</div>
										</td>
									</tr>
								</xsl:if>
							</xsl:when>
							<xsl:otherwise>
								<!-- Old style: render a list and assume that the files are there -->
								<xsl:if test="$output-channel-type = 'pdf'">
									<tr>
										<td class="col1">
											<xsl:value-of select="name"/>
										</td>
										<xsl:if test="$main-or-sub = 'main'">
											<td class="col2">
												<a href="#" target="iframe-system" class="btn btn-xs disabled btn-primary">
													<xsl:choose>
														<xsl:when test="$output-channel-type = 'website'"/>
														<xsl:otherwise>
															<xsl:attribute name="href">
																<xsl:value-of select="concat($url-cache-viewer, '&amp;tagname=', $tag-name, '&amp;filename=', @id, '-', $language-variant, '.pdf')"/>
															</xsl:attribute>
														</xsl:otherwise>
													</xsl:choose>
													
													
													<xsl:attribute name="class">
														<xsl:choose>
															<xsl:when test="$output-channel-type = 'website'">
																<xsl:text>btn btn-xs disabled btn-grey</xsl:text>
															</xsl:when>
															<xsl:otherwise>
																<xsl:text>btn btn-xs btn-primary</xsl:text>
															</xsl:otherwise>
														</xsl:choose>
													</xsl:attribute>
													
													<xsl:value-of select="$output-channel-name"/>
												</a>
											</td>
										</xsl:if>
										
										<td class="col3">
											<div class="pull-right">
												<xsl:if test="not($tag-name = $first-verion)">
													<button class="btn btn-xs btn-info" data-outputchanneltype="{$output-channel-type}" data-ocvariantid="{@id}" onclick="openCompareVersionModalDialog({$javascript-object}, {$params_diff_pdf}, '{name}', '{$output-channel-type}')">
														<xsl:if test="$output-channel-type = 'website'">
															<xsl:attribute name="disabled">true</xsl:attribute>
														</xsl:if>
														<xsl:text>Compare</xsl:text>
													</button>
												</xsl:if>
											</div>
										</td>
									</tr>
								</xsl:if>

							</xsl:otherwise>
						</xsl:choose>




					</xsl:for-each>

				</xsl:for-each>

			</tbody>
		</table>
		
		<xsl:if test="$nodetag/message/annotation and $main-or-sub = 'main'">
			<xsl:variable name="bundle-filename">
				<xsl:value-of select="$projectId"/>
				<xsl:text>-</xsl:text>
				<xsl:call-template name="string-replace-all">
					<xsl:with-param name="text" select="$tag-name"/>
					<xsl:with-param name="replace">.</xsl:with-param>
					<xsl:with-param name="by"></xsl:with-param>
				</xsl:call-template>
				<xsl:text>-bundle.zip</xsl:text>
			</xsl:variable>
			<a href="#" target="iframe-system" class="btn btn-xs btn-info">
				<xsl:choose>
					<xsl:when test="@exists = 'false'"/>
					<xsl:otherwise>
						<xsl:attribute name="href">
							<xsl:value-of select="concat($url-cache-viewer, '&amp;tagname=', $tag-name, '&amp;filename=', $bundle-filename)"/>
						</xsl:attribute>
					</xsl:otherwise>
				</xsl:choose>
								
				<xsl:choose>
					<xsl:when test="@exists = 'false'"/>
					<xsl:otherwise>
						<span class="glyphicon glyphicon-download"> </span>
					</xsl:otherwise>
				</xsl:choose>
				<xsl:text> Download bundle</xsl:text>
			</a>
		</xsl:if>

	</xsl:template>




	<xsl:template name="get-file-name">
		<xsl:param name="input"/>

		<xsl:choose>
			<xsl:when test="contains($input, '/')">
				<xsl:call-template name="get-file-name">
					<xsl:with-param name="input" select="substring-after($input, '/')"/>
				</xsl:call-template>
			</xsl:when>
			<xsl:otherwise>
				<xsl:value-of select="$input"/>
			</xsl:otherwise>
		</xsl:choose>

	</xsl:template>



</xsl:stylesheet>
